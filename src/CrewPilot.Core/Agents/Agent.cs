namespace CrewPilot.Core.Agents;

using CrewPilot.Core.Common;
using CrewPilot.Core.Mcp;
using CrewPilot.Core.Providers;
using CrewPilot.Core.Skills;

/// <summary>Agent 数字员工：岗位 + 模型 + 系统提示词 + 工具权限。</summary>
public class Agent : Entity
{
    public string Name { get; set; } = string.Empty;

    public AgentJob Job { get; set; }

    public Guid ProviderId { get; set; }

    public ModelProvider Provider { get; set; } = null!;

    /// <summary>模型标识（须在供应商目录中）。</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>系统提示词，支持 {{requirement}} 等模板变量。</summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>可用工具（JSON 数组：read_file/write_file/list_files/run_command）。</summary>
    public string? Tools { get; set; }

    /// <summary>终端命令白名单（JSON 数组，仅 run_command 工具生效）。</summary>
    public string? CommandWhitelist { get; set; }

    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AgentRun> Runs { get; set; } = new List<AgentRun>();

    /// <summary>装配的 Skills。</summary>
    public ICollection<AgentSkill> Skills { get; set; } = new List<AgentSkill>();

    /// <summary>授权的 MCP Server。</summary>
    public ICollection<AgentMcpServer> McpServers { get; set; } = new List<AgentMcpServer>();
}
