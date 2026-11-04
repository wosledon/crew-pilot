using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrewPilot.Infrastructure.Mcp;

/// <summary>MCP 工具描述（tools/list 结果）。</summary>
public sealed record McpToolInfo(string Name, string? Description, string? InputSchemaJson);

/// <summary>
/// 最小 MCP 客户端：Streamable HTTP（JSON-RPC over POST）实现 initialize / tools/list / tools/call。
/// 支持 JSON 与 SSE 两种响应体；认证通过 Bearer Token；不依赖第三方 MCP SDK。
/// 说明：stdio / 旧版 HTTP+SSE 双通道不在支持范围（配置可保存，运行时跳过）。
/// </summary>
public sealed class McpClient : IMcpSession
{
    private const string ProtocolVersion = "2024-11-05";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string? _authToken;
    private readonly bool _ownsHttpClient;
    private string? _sessionId;
    private int _requestId;

    private McpClient(HttpClient http, string endpoint, string? authToken, bool ownsHttpClient)
    {
        _http = http;
        _endpoint = endpoint;
        _authToken = authToken;
        _ownsHttpClient = ownsHttpClient;
    }

    public IReadOnlyList<McpToolInfo> Tools { get; private set; } = [];

    /// <summary>建立连接并拉取工具列表；失败抛出异常由调用方降级处理。</summary>
    public static async Task<McpClient> ConnectAsync(
        string endpoint,
        string? authToken = null,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var http = new HttpClient { Timeout = timeout ?? TimeSpan.FromSeconds(15) };
        var client = new McpClient(http, endpoint, authToken, ownsHttpClient: true);
        return await ConnectCoreAsync(client, ct);
    }

    /// <summary>用外部注入的 HttpClient 建立连接（便于测试与连接池复用）。</summary>
    public static async Task<McpClient> ConnectAsync(
        HttpClient httpClient,
        string endpoint,
        string? authToken = null,
        CancellationToken ct = default)
    {
        var client = new McpClient(httpClient, endpoint, authToken, ownsHttpClient: false);
        return await ConnectCoreAsync(client, ct);
    }

    private static async Task<McpClient> ConnectCoreAsync(McpClient client, CancellationToken ct)
    {
        try
        {
            await client.InitializeAsync(ct);
            client.Tools = await client.ListToolsAsync(ct);
            return client;
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }
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

        using var response = await SendAsync(
            "tools/call",
            new { name = toolName, arguments },
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

    private async Task InitializeAsync(CancellationToken ct)
    {
        using var response = await SendAsync(
            "initialize",
            new
            {
                protocolVersion = ProtocolVersion,
                capabilities = new { },
                clientInfo = new { name = "CrewPilot", version = "0.1.0" },
            },
            ct);

        if (response.RootElement.TryGetProperty("error", out var error))
        {
            throw new InvalidOperationException($"MCP initialize 失败：{error.GetRawText()}");
        }

        // 初始化完成通知（无需响应）
        await SendNotificationAsync("notifications/initialized", ct);
    }

    private async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct)
    {
        using var response = await SendAsync("tools/list", new { }, ct);
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

    private async Task SendNotificationAsync(string method, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { jsonrpc = "2.0", method }, JsonOptions);
        using var request = CreateRequest(payload);
        using var response = await _http.SendAsync(request, ct);
        // 通知允许 202/204 或任意非错误响应
    }

    private async Task<JsonDocument> SendAsync(string method, object? parameters, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _requestId);
        var payload = JsonSerializer.Serialize(
            new { jsonrpc = "2.0", id, method, @params = parameters },
            JsonOptions);

        using var request = CreateRequest(payload);
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        if (response.Headers.TryGetValues("Mcp-Session-Id", out var sessionValues))
        {
            _sessionId = sessionValues.FirstOrDefault();
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var body = await response.Content.ReadAsStringAsync(ct);
        var json = contentType.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase)
            ? ExtractSseMessage(body, id)
            : body;

        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"MCP 响应不是合法 JSON：{body}", ex);
        }
    }

    private HttpRequestMessage CreateRequest(string payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (!string.IsNullOrWhiteSpace(_authToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
        }

        if (!string.IsNullOrWhiteSpace(_sessionId))
        {
            request.Headers.Add("Mcp-Session-Id", _sessionId);
        }

        return request;
    }

    /// <summary>从 SSE 流中提取 id 匹配的 data JSON（忽略通知事件）。</summary>
    private static string ExtractSseMessage(string body, int id)
    {
        string? fallback = null;
        foreach (var rawLine in body.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (string.IsNullOrWhiteSpace(data) || data == "[DONE]")
            {
                continue;
            }

            fallback ??= data;
            try
            {
                using var document = JsonDocument.Parse(data);
                if (document.RootElement.TryGetProperty("id", out var idElement) &&
                    idElement.ValueKind == JsonValueKind.Number &&
                    idElement.GetInt32() == id)
                {
                    return data;
                }
            }
            catch (JsonException)
            {
                // 非 JSON 心跳/注释，忽略
            }
        }

        return fallback ?? "{}";
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
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }

        await Task.CompletedTask;
    }
}
