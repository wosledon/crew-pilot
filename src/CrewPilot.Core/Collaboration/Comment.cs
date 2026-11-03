namespace CrewPilot.Core.Collaboration;

using CrewPilot.Core.Common;

/// <summary>工作项评论（@Agent 可触发其执行）。</summary>
public class Comment : Entity
{
    public Guid WorkItemId { get; set; }

    public WorkItems.WorkItem WorkItem { get; set; } = null!;

    public Guid AuthorId { get; set; }

    public Projects.ProjectMember Author { get; set; } = null!;

    /// <summary>作者类型：用户或 Agent，用于时间线区分。</summary>
    public Projects.MemberKind AuthorKind { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
