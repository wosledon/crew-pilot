namespace CrewPilot.Core.Mcp;

using CrewPilot.Core.Agents;
using CrewPilot.Core.Common;

/// <summary>MCP Server 配置（PRD 5.7）：地址、认证、启停；认证 Token 加密存储。</summary>
public class McpServer : Entity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>MCP Server 地址（HTTP/SSE 端点，或 stdio 启动命令）。</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>传输方式：http / sse / stdio。</summary>
    public string Transport { get; set; } = "http";

    /// <summary>加密后的认证 Token（可选）。</summary>
    public string? AuthTokenProtected { get; set; }

    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AgentMcpServer> Agents { get; set; } = new List<AgentMcpServer>();
}

/// <summary>Agent ↔ MCP Server 授权关系。</summary>
public class AgentMcpServer : Entity
{
    public Guid AgentId { get; set; }

    public Agent Agent { get; set; } = null!;

    public Guid McpServerId { get; set; }

    public McpServer McpServer { get; set; } = null!;
}
