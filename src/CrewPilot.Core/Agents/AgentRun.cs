namespace CrewPilot.Core.Agents;

using CrewPilot.Core.Common;
using CrewPilot.Core.WorkItems;

/// <summary>
/// AgentRun：某 Agent 在某工作项上的一次执行。
/// 全程留痕（日志/产物/工时与 token 成本），可随时取消（被接管）。
/// </summary>
public class AgentRun : Entity
{
    public Guid AgentId { get; set; }

    public Agent Agent { get; set; } = null!;

    /// <summary>被认领的工作项（手动试运行时为空）。</summary>
    public Guid? WorkItemId { get; set; }

    public WorkItem? WorkItem { get; set; }

    /// <summary>由工作流触发时的实例 Id。</summary>
    public Guid? WorkflowRunId { get; set; }

    public Workflows.WorkflowRun? WorkflowRun { get; set; }

    /// <summary>执行的工作流步骤 Id。</summary>
    public Guid? WorkflowStepId { get; set; }

    public Workflows.WorkflowStep? WorkflowStep { get; set; }

    /// <summary>触发原因，如 manual / assigned / workflow / mention。</summary>
    public string Trigger { get; set; } = "manual";

    /// <summary>执行时的工作目录（项目目录隔离 / worktree）。</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>实际使用的提示词快照。</summary>
    public string Prompt { get; set; } = string.Empty;

    public AgentRunStatus Status { get; set; } = AgentRunStatus.Queued;

    public string? Output { get; set; }

    public string? Error { get; set; }

    public int? PromptTokens { get; set; }

    public int? CompletionTokens { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>执行耗时（1:1 折算工时的依据）。</summary>
    public TimeSpan? Duration { get; set; }

    public ICollection<RunLog> Logs { get; set; } = new List<RunLog>();
}
