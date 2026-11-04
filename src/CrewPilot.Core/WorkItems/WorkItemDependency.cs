namespace CrewPilot.Core.WorkItems;

using CrewPilot.Core.Common;

/// <summary>工作项依赖类型：完成-开始 / 开始-开始。</summary>
public enum DependencyType
{
    /// <summary>前置完成 → 后置开始。</summary>
    FinishToStart,

    /// <summary>前置开始 → 后置开始。</summary>
    StartToStart,
}

/// <summary>工作项依赖：用于甘特图排程与关键路径。</summary>
public class WorkItemDependency : Entity
{
    public Guid ProjectId { get; set; }

    public Guid FromWorkItemId { get; set; }

    public WorkItem FromWorkItem { get; set; } = null!;

    public Guid ToWorkItemId { get; set; }

    public WorkItem ToWorkItem { get; set; } = null!;

    public DependencyType Type { get; set; } = DependencyType.FinishToStart;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
