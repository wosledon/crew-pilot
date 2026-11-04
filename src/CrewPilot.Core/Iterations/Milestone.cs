namespace CrewPilot.Core.Iterations;

using CrewPilot.Core.Common;

/// <summary>里程碑：甘特图与版本计划的时间锚点。</summary>
public class Milestone : Entity
{
    public Guid ProjectId { get; set; }

    public Projects.Project Project { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly Date { get; set; }

    public ICollection<Iteration> Iterations { get; set; } = new List<Iteration>();
}
