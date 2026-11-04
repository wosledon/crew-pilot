namespace CrewPilot.Core.Projects;

using CrewPilot.Core.Common;

/// <summary>项目成员：人类用户与 Agent 统一建模，Agent 成员带执行策略。</summary>
public class ProjectMember : Entity
{
    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public MemberKind Kind { get; set; }

    public ProjectRole Role { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>时薪（可选，用于人力成本报表；币种由用户约定）。</summary>
    public decimal? HourlyRate { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
