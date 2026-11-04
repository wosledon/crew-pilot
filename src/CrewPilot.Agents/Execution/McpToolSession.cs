using System.Text.RegularExpressions;
using CrewPilot.Agents.Abstractions;
using CrewPilot.Core.Abstractions;
using CrewPilot.Core.Mcp;
using CrewPilot.Infrastructure.Mcp;

namespace CrewPilot.Agents.Execution;

/// <summary>
/// 一次 AgentRun 的 MCP 工具会话：
/// 按绑定的 MCP Server 建连、聚合工具（名称加服务器前缀避免冲突），运行结束释放连接。
/// 单个 Server 连接失败只跳过该 Server，不影响 AgentRun。
/// </summary>
public sealed class McpToolSession : IAsyncDisposable
{
    private static readonly Regex InvalidToolNameChars = new(
        "[^a-zA-Z0-9_-]", RegexOptions.Compiled);

    private readonly List<McpClient> _clients = [];
    private readonly Dictionary<string, (McpClient Client, string RemoteName)> _routes =
        new(StringComparer.OrdinalIgnoreCase);

    private McpToolSession()
    {
    }

    public IReadOnlyList<McpToolDescriptor> Tools { get; private set; } = [];

    public IMcpToolInvoker? Invoker { get; private set; }

    /// <summary>根据 Agent 绑定的 MCP Server 建立会话；无绑定/全部失败时返回空会话。</summary>
    public static async Task<McpToolSession> ConnectAsync(
        IEnumerable<AgentMcpServer> bindings,
        ISecretProtector secrets,
        CancellationToken ct = default)
    {
        var session = new McpToolSession();
        var descriptors = new List<McpToolDescriptor>();

        foreach (var binding in bindings)
        {
            var server = binding.McpServer;
            if (server is null || !server.Enabled)
            {
                continue;
            }

            // 运行时仅支持 Streamable HTTP；stdio / 旧版 SSE 双通道跳过
            if (!string.Equals(server.Transport, "http", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(server.Transport, "sse", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var token = string.IsNullOrWhiteSpace(server.AuthTokenProtected)
                    ? null
                    : secrets.Unprotect(server.AuthTokenProtected);
                var client = await McpClient.ConnectAsync(
                    server.Endpoint, token, TimeSpan.FromSeconds(10), ct);
                session._clients.Add(client);

                foreach (var tool in client.Tools)
                {
                    var name = BuildToolName(server, tool.Name);
                    if (session._routes.ContainsKey(name))
                    {
                        continue;
                    }

                    session._routes[name] = (client, tool.Name);
                    descriptors.Add(new McpToolDescriptor(
                        name, tool.Description, tool.InputSchemaJson));
                }
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // MCP 不可用：跳过，Agent 仍可用内置工具执行
            }
        }

        session.Tools = descriptors;
        if (session._routes.Count > 0)
        {
            session.Invoker = new RouteInvoker(session._routes);
        }

        return session;
    }

    private static string BuildToolName(McpServer server, string toolName)
    {
        var serverSlug = InvalidToolNameChars.Replace(server.Name, "_");
        var combined = $"mcp_{serverSlug}_{toolName}";
        var sanitized = InvalidToolNameChars.Replace(combined, "_");
        return sanitized.Length <= 64 ? sanitized : sanitized[..64];
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            await client.DisposeAsync();
        }

        _clients.Clear();
        _routes.Clear();
        Tools = [];
        Invoker = null;
    }

    private sealed class RouteInvoker(
        IReadOnlyDictionary<string, (McpClient Client, string RemoteName)> routes)
        : IMcpToolInvoker
    {
        public Task<string> CallToolAsync(
            string toolName, string? argumentsJson, CancellationToken cancellationToken = default)
        {
            return routes.TryGetValue(toolName, out var route)
                ? route.Client.CallToolAsync(route.RemoteName, argumentsJson, cancellationToken)
                : Task.FromResult($"未知 MCP 工具：{toolName}");
        }
    }
}
