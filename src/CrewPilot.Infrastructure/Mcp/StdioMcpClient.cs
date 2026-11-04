using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrewPilot.Infrastructure.Mcp;

/// <summary>
/// MCP stdio 传输客户端：启动本地子进程，通过换行分隔的 JSON-RPC 通信。
/// 命令由 Owner 在 MCP 配置中显式填写（例如 npx -y @modelcontextprotocol/server-filesystem /path）；
/// 进程退出/超时抛异常，由上层降级。
/// </summary>
public sealed class StdioMcpClient : IMcpSession
{
    private const string ProtocolVersion = "2024-11-05";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Process? _process;
    private readonly TextReader _stdout;
    private readonly TextWriter _stdin;
    private readonly Func<Task>? _shutdown;
    private int _requestId;

    private StdioMcpClient(
        Process? process, TextReader stdout, TextWriter stdin, Func<Task>? shutdown)
    {
        _process = process;
        _stdout = stdout;
        _stdin = stdin;
        _shutdown = shutdown;
    }

    public IReadOnlyList<McpToolInfo> Tools { get; private set; } = [];

    /// <summary>启动进程并完成初始化 / 工具发现。</summary>
    public static async Task<StdioMcpClient> ConnectAsync(
        string commandLine,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var tokens = Tokenize(commandLine);
        if (tokens.Count == 0)
        {
            throw new InvalidOperationException("MCP stdio 命令为空");
        }

        var psi = new ProcessStartInfo(tokens[0])
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in tokens.Skip(1))
        {
            psi.ArgumentList.Add(argument);
        }

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"MCP stdio 进程启动失败：{tokens[0]}");

        // 持续排空 stderr，避免子进程因缓冲区写满阻塞
        _ = Task.Run(async () =>
        {
            try
            {
                while (await process.StandardError.ReadLineAsync() is not null)
                {
                }
            }
            catch (Exception)
            {
                // 进程退出时忽略
            }
        });

        var client = new StdioMcpClient(
            process,
            process.StandardOutput,
            process.StandardInput,
            async () =>
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                await process.WaitForExitAsync();
                process.Dispose();
            });

        try
        {
            await client.InitializeAsync(timeout ?? TimeSpan.FromSeconds(30), ct);
            client.Tools = await client.ListToolsAsync(ct);
            return client;
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }
    }

    /// <summary>用内存流构造（单元测试：验证 JSON-RPC 行协议）。</summary>
    public static async Task<StdioMcpClient> ConnectAsync(
        TextReader stdout, TextWriter stdin, CancellationToken ct = default)
    {
        var client = new StdioMcpClient(null, stdout, stdin, null);
        await client.InitializeAsync(TimeSpan.FromSeconds(5), ct);
        client.Tools = await client.ListToolsAsync(ct);
        return client;
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
            "tools/call", new { name = toolName, arguments }, ct);
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

    public async ValueTask DisposeAsync()
    {
        if (_shutdown is not null)
        {
            try
            {
                await _shutdown();
            }
            catch (Exception)
            {
                // 已退出 / 平台差异忽略
            }

            return;
        }

        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process?.Dispose();
        _stdout.Dispose();
        _stdin.Dispose();
    }

    private async Task InitializeAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var response = await SendAsync(
            "initialize",
            new
            {
                protocolVersion = ProtocolVersion,
                capabilities = new { },
                clientInfo = new { name = "CrewPilot", version = "0.1.0" },
            },
            ct,
            timeout);

        if (response.RootElement.TryGetProperty("error", out var error))
        {
            throw new InvalidOperationException($"MCP stdio initialize 失败：{error.GetRawText()}");
        }

        await _stdin.WriteLineAsync(
            JsonSerializer.Serialize(
                new { jsonrpc = "2.0", method = "notifications/initialized" }, JsonOptions));
        await _stdin.FlushAsync(ct);
    }

    private async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct)
    {
        using var response = await SendAsync("tools/list", new { }, ct);
        if (response.RootElement.TryGetProperty("error", out var error))
        {
            throw new InvalidOperationException($"MCP stdio tools/list 失败：{error.GetRawText()}");
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

    private async Task<JsonDocument> SendAsync(
        string method,
        object? parameters,
        CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var id = Interlocked.Increment(ref _requestId);
        var payload = JsonSerializer.Serialize(
            new { jsonrpc = "2.0", id, method, @params = parameters }, JsonOptions);
        await _stdin.WriteLineAsync(payload);
        await _stdin.FlushAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout ?? TimeSpan.FromSeconds(60));

        while (true)
        {
            var line = await _stdout.ReadLineAsync(timeoutCts.Token);
            if (line is null)
            {
                throw new InvalidOperationException("MCP stdio 进程已退出或输出流已关闭");
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                // 忽略非 JSON 输出（日志/横幅）
                continue;
            }

            if (!document.RootElement.TryGetProperty("id", out var idElement) ||
                !idElement.TryGetInt32(out var responseId) ||
                responseId != id)
            {
                document.Dispose();
                continue;
            }

            return document;
        }
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

    /// <summary>命令行拆分（支持引号；不经过 shell）。</summary>
    public static IReadOnlyList<string> Tokenize(string commandLine)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inDoubleQuote = false;
        var inSingleQuote = false;

        foreach (var ch in commandLine)
        {
            if (ch == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
            }
            else if (ch == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
            }
            else if (char.IsWhiteSpace(ch) && !inDoubleQuote && !inSingleQuote)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
