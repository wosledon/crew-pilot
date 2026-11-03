namespace CrewPilot.Core.WorkLogs;

using CrewPilot.Core.Common;

/// <summary>工时登记。AgentRun 结束后按 1:1 自动折算登记；用户也可手动补登。</summary>
public class WorkLog : Entity
{
    public Guid WorkItemId { get; set; }

    public WorkItems.WorkItem WorkItem { get; set; } = null!;

    /// <summary>登记人（用户或 Agent），系统自动折算时可为空。</summary>
    public Guid? MemberId { get; set; }

    public Projects.ProjectMember? Member { get; set; }

    /// <summary>工时（小时）。</summary>
    public decimal Hours { get; set; }

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public string? Note { get; set; }

    /// <summary>来源 AgentRun（自动折算时回填）。</summary>
    public Guid? AgentRunId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
