namespace CrewPilot.Infrastructure.Mcp;

/// <summary>MCP 会话抽象：HTTP / stdio 两种传输的统一接口。</summary>
public interface IMcpSession : IAsyncDisposable
{
    IReadOnlyList<McpToolInfo> Tools { get; }

    Task<string> CallToolAsync(
        string toolName, string? argumentsJson, CancellationToken ct = default);
}
