namespace CrewPilot.Core.Iterations;

using CrewPilot.Core.Common;

/// <summary>迭代状态。</summary>
public enum IterationStatus
{
    Planned,
    Active,
    Completed,
}

/// <summary>迭代（Sprint）。</summary>
public class Iteration : Entity
{
    public Guid ProjectId { get; set; }

    public Projects.Project Project { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string? Goal { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public IterationStatus Status { get; set; } = IterationStatus.Planned;

    public ICollection<WorkItems.WorkItem> WorkItems { get; set; } = new List<WorkItems.WorkItem>();
}
