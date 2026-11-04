namespace CrewPilot.Core.Agents;

/// <summary>AgentRun 状态。</summary>
public enum AgentRunStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Canceled,

    /// <summary>输出已产生，但工作流卡点等待用户处理。</summary>
    WaitingForHuman,
}
