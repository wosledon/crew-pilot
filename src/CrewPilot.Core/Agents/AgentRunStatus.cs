namespace CrewPilot.Core.Agents;

/// <summary>AgentRun 状态。</summary>
public enum AgentRunStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Canceled,
}
