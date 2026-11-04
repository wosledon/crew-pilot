namespace CrewPilot.Core.Agents;

using CrewPilot.Core.Common;

/// <summary>Agent 团队：一组 Agent + 团队目标，可整体指派给项目/工作项类型（PRD 5.3）。</summary>
public class AgentTeam : Entity
{
    public Guid ProjectId { get; set; }

    public Projects.Project Project { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string? Goal { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AgentTeamMember> Members { get; set; } = new List<AgentTeamMember>();
}

public class AgentTeamMember : Entity
{
    public Guid TeamId { get; set; }

    public AgentTeam Team { get; set; } = null!;

    public Guid AgentId { get; set; }

    public Agent Agent { get; set; } = null!;

    /// <summary>团队内角色（如 owner/reviewer），可选。</summary>
    public string? Role { get; set; }
}
