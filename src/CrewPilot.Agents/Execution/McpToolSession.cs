using System.Collections.Concurrent;
using CrewPilot.Agents.Abstractions;
using CrewPilot.Core.Abstractions;
using CrewPilot.Core.Mcp;
using CrewPilot.Infrastructure.Mcp;

namespace CrewPilot.Agents.Execution;

/// <summary>
/// 一次 AgentRun 的 MCP 工具会话：
/// 按绑定的 MCP Server 建连、聚合工具（名称加服务器前缀避免冲突），运行结束释放连接。
/// 单个 Server 连接失败只跳过该 Server，不影响 AgentRun。
/// 所有工具调用记录到审计队列，由 <see cref="AgentRunExecutor"/> 在运行结束后统一落库。
/// </summary>
public sealed class McpToolSession : IAsyncDisposable
{
    private readonly List<IMcpSession> _clients = [];
    private readonly Dictionary<string, McpRoute> _routes =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<McpToolCallAudit> _calls = new();

    private McpToolSession()
    {
    }

    public IReadOnlyList<McpToolDescriptor> Tools { get; private set; } = [];

    public IMcpToolInvoker? Invoker { get; private set; }

    /// <summary>本次运行内已发生的 MCP 工具调用审计（调用发生顺序）。</summary>
    public IReadOnlyList<McpToolCallAudit> Calls => _calls.ToArray();

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

            try
            {
                var token = string.IsNullOrWhiteSpace(server.AuthTokenProtected)
                    ? null
                    : secrets.Unprotect(server.AuthTokenProtected);
                var isStdio = string.Equals(
                    server.Transport, "stdio", StringComparison.OrdinalIgnoreCase);
                var isLegacySse = string.Equals(
                    server.Transport, "sse", StringComparison.OrdinalIgnoreCase);
                IMcpSession client = isStdio
                    ? await StdioMcpClient.ConnectAsync(
                        server.Endpoint, TimeSpan.FromSeconds(30), ct)
                    : isLegacySse
                        ? await SseMcpClient.ConnectAsync(
                            server.Endpoint, token, TimeSpan.FromSeconds(15), ct)
                        : await McpClient.ConnectAsync(
                            server.Endpoint, token, TimeSpan.FromSeconds(10), ct);
                session._clients.Add(client);

                foreach (var tool in client.Tools)
                {
                    var name = McpRouteInvoker.SanitizeToolName(
                        $"mcp_{server.Name}_{tool.Name}");
                    if (session._routes.ContainsKey(name))
                    {
                        continue;
                    }

                    session._routes[name] = new McpRoute(
                        client, tool.Name, server.Name, tool.Name);
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
            session.Invoker = new McpRouteInvoker(session._routes, session._calls);
        }

        return session;
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
}
