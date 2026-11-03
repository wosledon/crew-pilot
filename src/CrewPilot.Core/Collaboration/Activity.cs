namespace CrewPilot.Core.Collaboration;

using CrewPilot.Core.Common;

/// <summary>活动流事件：状态变更、指派、AgentRun、卡点等全量记录。</summary>
public class Activity : Entity
{
    public Guid ProjectId { get; set; }

    public Projects.Project? Project { get; set; }

    public Guid? WorkItemId { get; set; }

    public WorkItems.WorkItem? WorkItem { get; set; }

    /// <summary>事件类型，如 workitem.status.changed。</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>事件负载（JSON）。</summary>
    public string Payload { get; set; } = "{}";

    public Guid? ActorId { get; set; }

    public Projects.ProjectMember? Actor { get; set; }

    public Projects.MemberKind? ActorKind { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
