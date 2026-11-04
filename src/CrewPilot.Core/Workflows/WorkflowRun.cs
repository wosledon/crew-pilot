namespace CrewPilot.Core.Workflows;

using CrewPilot.Core.Agents;
using CrewPilot.Core.Common;
using CrewPilot.Core.WorkItems;

/// <summary>工作流实例状态。</summary>
public enum WorkflowRunStatus
{
    Running,

    /// <summary>等待用户处理卡点。</summary>
    WaitingForHuman,

    Succeeded,

    Failed,

    Canceled,
}

/// <summary>卡点类型：Agent 主动上报、步骤审批、执行失败。</summary>
public enum BlockerKind
{
    /// <summary>Agent 无法自行决策（输出 [[BLOCKER]] 标记）。</summary>
    AgentBlocked,

    /// <summary>审批策略为「总是询问」的步骤完成确认。</summary>
    Approval,

    /// <summary>步骤执行失败且重试耗尽。</summary>
    Failure,
}

/// <summary>卡点处理结果。</summary>
public enum BlockerResolution
{
    Continue,

    Redirect,

    Cancel,
}

public enum BlockerStatus
{
    Open,
    Resolved,
}

/// <summary>工作流一次实例化运行：某个工作项走完一条工作流。</summary>
public class WorkflowRun : Entity
{
    public Guid WorkflowId { get; set; }

    public Workflow Workflow { get; set; } = null!;

    public Guid WorkItemId { get; set; }

    public WorkItem WorkItem { get; set; } = null!;

    public WorkflowRunStatus Status { get; set; } = WorkflowRunStatus.Running;

    /// <summary>当前步骤 Order（0 表示第一步）。</summary>
    public int CurrentStepOrder { get; set; }

    public Guid? CurrentStepId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }
}

/// <summary>卡点：工作流挂起项，集中到用户的卡点队列处理。</summary>
public class Blocker : Entity
{
    public Guid ProjectId { get; set; }

    public Guid WorkItemId { get; set; }

    public WorkItem WorkItem { get; set; } = null!;

    public Guid? WorkflowRunId { get; set; }

    public WorkflowRun? WorkflowRun { get; set; }

    public Guid? AgentRunId { get; set; }

    public AgentRun? AgentRun { get; set; }

    public BlockerKind Kind { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>上下文快照（工作项、步骤、Agent 输出摘要）。</summary>
    public string? ContextSnapshot { get; set; }

    public BlockerStatus Status { get; set; } = BlockerStatus.Open;

    public BlockerResolution? Resolution { get; set; }

    public string? ResolutionNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ResolvedAt { get; set; }
}
