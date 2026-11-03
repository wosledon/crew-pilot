namespace CrewPilot.Core.Agents;

using CrewPilot.Core.Common;

/// <summary>AgentRun 日志条目：中间步骤、工具调用、最终输出。</summary>
public class RunLog : Entity
{
    public Guid AgentRunId { get; set; }

    public AgentRun AgentRun { get; set; } = null!;

    public int Sequence { get; set; }

    /// <summary>日志类型：info / tool / assistant / error。</summary>
    public string Kind { get; set; } = "info";

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
