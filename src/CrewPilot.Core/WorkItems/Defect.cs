namespace CrewPilot.Core.WorkItems;

/// <summary>缺陷。</summary>
public class Defect : WorkItem
{
    public DefectStatus Status { get; set; } = DefectStatus.New;

    public Severity Severity { get; set; } = Severity.Normal;

    public string? ReproSteps { get; set; }

    public string? Environment { get; set; }

    /// <summary>关联的根因需求（可选）。</summary>
    public Guid? RelatedRequirementId { get; set; }
}
