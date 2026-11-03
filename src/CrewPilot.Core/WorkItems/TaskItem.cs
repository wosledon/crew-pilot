namespace CrewPilot.Core.WorkItems;

/// <summary>任务：需求/缺陷拆解出的最小执行单元。</summary>
public class TaskItem : WorkItem
{
    public Guid ParentWorkItemId { get; set; }

    public bool IsDone { get; set; }
}
