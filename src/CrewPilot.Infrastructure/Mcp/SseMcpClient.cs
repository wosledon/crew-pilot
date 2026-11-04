using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrewPilot.Infrastructure.Mcp;

/// <summary>
/// 旧版 MCP HTTP+SSE 传输（2024-11-05 双向通道）：
/// GET 建立长连接 SSE，服务端先下发 endpoint 事件，之后客户端 POST 消息、响应经 SSE 回流。
/// 用于兼容旧版 MCP Server；新实现优先 Streamable HTTP（McpClient）。
/// </summary>
public sealed class SseMcpClient : IMcpSession
{
    private const string ProtocolVersion = "2024-11-05";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _http;
    private readonly string _sseEndpoint;
    private readonly string? _authToken;
    private readonly bool _ownsHttpClient;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonDocument>> _pending = new();
    private readonly TaskCompletionSource<Uri> _messageEndpoint =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _lifetime = new();
    private int _requestId;
    private Task? _readerTask;
    private HttpResponseMessage? _sseResponse;

    private SseMcpClient(HttpClient http, string sseEndpoint, string? authToken, bool ownsHttpClient)
    {
        _http = http;
        _sseEndpoint = sseEndpoint;
        _authToken = authToken;
        _ownsHttpClient = ownsHttpClient;
    }

    public IReadOnlyList<McpToolInfo> Tools { get; private set; } = [];

    public static async Task<SseMcpClient> ConnectAsync(
        string endpoint,
        string? authToken = null,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        var client = new SseMcpClient(http, endpoint, authToken, ownsHttpClient: true);
        return await ConnectCoreAsync(client, timeout ?? TimeSpan.FromSeconds(15), ct);
    }

    public static async Task<SseMcpClient> ConnectAsync(
        HttpClient httpClient,
        string endpoint,
        string? authToken = null,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var client = new SseMcpClient(httpClient, endpoint, authToken, ownsHttpClient: false);
        return await ConnectCoreAsync(client, timeout ?? TimeSpan.FromSeconds(15), ct);
    }

    private static async Task<SseMcpClient> ConnectCoreAsync(
        SseMcpClient client, TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            await client.StartSseAsync(timeout, ct);
            await client.InitializeAsync(timeout, ct);
            client.Tools = await client.ListToolsAsync(timeout, ct);
            return client;
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }
    }

    private async Task StartSseAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _sseEndpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (!string.IsNullOrWhiteSpace(_authToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _authToken);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        _sseResponse = await _http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
        _sseResponse.EnsureSuccessStatusCode();
        var stream = await _sseResponse.Content.ReadAsStreamAsync(timeoutCts.Token);
        _readerTask = Task.Run(
            () => ReadEventsAsync(new StreamReader(stream, Encoding.UTF8), _lifetime.Token),
            CancellationToken.None);
        await _messageEndpoint.Task.WaitAsync(timeoutCts.Token);
    }

    private async Task ReadEventsAsync(StreamReader reader, CancellationToken ct)
    {
        string? eventName = null;
        var data = new StringBuilder();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (line is null)
                {
                    return;
                }

                if (line.Length == 0)
                {
                    Dispatch(eventName, data.ToString());
                    eventName = null;
                    data.Clear();
                    continue;
                }

                if (line.StartsWith(':'))
                {
                    continue; // 心跳/注释
                }

                var separator = line.IndexOf(':');
                var field = separator < 0 ? line : line[..separator];
                var value = separator < 0 ? string.Empty : line[(separator + 1)..].TrimStart();
                if (field == "event")
                {
                    eventName = value;
                }
                else if (field == "data")
                {
                    if (data.Length > 0)
                    {
                        data.Append('\n');
                    }

                    data.Append(value);
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // 会话释放
        }
    }

    private void Dispatch(string? eventName, string data)
    {
        if (string.IsNullOrEmpty(eventName) || string.IsNullOrWhiteSpace(data))
        {
            return;
        }

        if (eventName == "endpoint")
        {
            if (Uri.TryCreate(_sseEndpoint, UriKind.Absolute, out var baseUri) &&
                Uri.TryCreate(baseUri, data, out var messageUri))
            {
                _messageEndpoint.TrySetResult(messageUri);
            }
            else
            {
                _messageEndpoint.TrySetException(
                    new InvalidOperationException($"MCP SSE endpoint 非法：{data}"));
            }

            return;
        }

        if (eventName != "message")
        {
            return;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(data);
        }
        catch (JsonException)
        {
            return;
        }

        if (document.RootElement.TryGetProperty("id", out var idElement) &&
            idElement.ValueKind == JsonValueKind.Number &&
            _pending.TryRemove(idElement.GetInt32(), out var completion))
        {
            completion.TrySetResult(document);
            return;
        }

        document.Dispose(); // 通知或未知消息
    }

    private async Task InitializeAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var response = await SendRequestAsync(
            "initialize",
            new
            {
                protocolVersion = ProtocolVersion,
                capabilities = new { },
                clientInfo = new { name = "CrewPilot", version = "0.1.0" },
            },
            timeout,
            ct);
        if (response.RootElement.TryGetProperty("error", out var error))
        {
            throw new InvalidOperationException($"MCP initialize 失败：{error.GetRawText()}");
        }

        await SendNotificationAsync("notifications/initialized", timeout, ct);
    }

    private async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(
        TimeSpan timeout, CancellationToken ct)
    {
        using var response = await SendRequestAsync("tools/list", new { }, timeout, ct);
        if (response.RootElement.TryGetProperty("error", out var error))
        {
            throw new InvalidOperationException($"MCP tools/list 失败：{error.GetRawText()}");
        }

        if (!response.RootElement.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("tools", out var tools) ||
            tools.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<McpToolInfo>();
        foreach (var tool in tools.EnumerateArray())
        {
            var name = tool.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            list.Add(new McpToolInfo(
                name,
                tool.TryGetProperty("description", out var description)
                    ? description.GetString()
                    : null,
                tool.TryGetProperty("inputSchema", out var schema) ? schema.GetRawText() : null));
        }

        return list;
    }

    public async Task<string> CallToolAsync(
        string toolName, string? argumentsJson, CancellationToken ct = default)
    {
        JsonNode arguments;
        try
        {
            arguments = string.IsNullOrWhiteSpace(argumentsJson)
                ? new JsonObject()
                : JsonNode.Parse(argumentsJson) ?? new JsonObject();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"MCP 参数不是合法 JSON：{argumentsJson}");
        }

        using var response = await SendRequestAsync(
            "tools/call",
            new { name = toolName, arguments },
            TimeSpan.FromSeconds(60),
            ct);
        var root = response.RootElement;
        if (root.TryGetProperty("error", out var error))
        {
            return $"MCP 工具调用失败：{error.GetRawText()}";
        }

        var result = root.GetProperty("result");
        var isError = result.TryGetProperty("isError", out var isErrorElement) &&
                      isErrorElement.ValueKind == JsonValueKind.True;
        var text = ExtractContentText(result);
        return isError ? $"MCP 工具返回错误：{text}" : text;
    }

    private async Task<JsonDocument> SendRequestAsync(
        string method, object? parameters, TimeSpan timeout, CancellationToken ct)
    {
        var messageUri = await _messageEndpoint.Task.WaitAsync(timeout, ct);
        var id = Interlocked.Increment(ref _requestId);
        var completion = new TaskCompletionSource<JsonDocument>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;

        try
        {
            await PostAsync(
                JsonSerializer.Serialize(
                    new { jsonrpc = "2.0", id, method, @params = parameters },
                    JsonOptions),
                messageUri,
                timeout,
                ct);
            return await completion.Task.WaitAsync(timeout, ct);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private async Task SendNotificationAsync(
        string method, TimeSpan timeout, CancellationToken ct)
    {
        var messageUri = await _messageEndpoint.Task.WaitAsync(timeout, ct);
        await PostAsync(
            JsonSerializer.Serialize(new { jsonrpc = "2.0", method }, JsonOptions),
            messageUri,
            timeout,
            ct);
    }

    private async Task PostAsync(
        string payload, Uri messageUri, TimeSpan timeout, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        using var request = new HttpRequestMessage(HttpMethod.Post, messageUri)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(_authToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _authToken);
        }

        using var response = await _http.SendAsync(request, timeoutCts.Token);
        response.EnsureSuccessStatusCode();
    }

    private static string ExtractContentText(JsonElement result)
    {
        if (!result.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.Array)
        {
            return result.GetRawText();
        }

        var builder = new StringBuilder();
        foreach (var item in content.EnumerateArray())
        {
            if (item.TryGetProperty("text", out var text) &&
                text.ValueKind == JsonValueKind.String)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(text.GetString());
            }
        }

        return builder.Length > 0 ? builder.ToString() : result.GetRawText();
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        if (_readerTask is not null)
        {
            try
            {
                await _readerTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (Exception) when (!_lifetime.IsCancellationRequested)
            {
                // reader 已异常退出，忽略
            }
            catch (TimeoutException)
            {
                // 读循环未及时退出（底层连接将由 HttpClient 释放）
            }
        }

        foreach (var pending in _pending.Values)
        {
            pending.TrySetCanceled();
        }

        _pending.Clear();
        _sseResponse?.Dispose();
        _lifetime.Dispose();
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }
}
