namespace CrewPilot.Core.WorkItems;

using CrewPilot.Core.Prototypes;

/// <summary>需求。</summary>
public class Requirement : WorkItem
{
    public RequirementStatus Status { get; set; } = RequirementStatus.Backlog;

    public ICollection<Prototype> Prototypes { get; set; } = new List<Prototype>();
}
