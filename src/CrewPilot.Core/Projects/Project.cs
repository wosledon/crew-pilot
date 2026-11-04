namespace CrewPilot.Core.Projects;

using CrewPilot.Core.Common;

/// <summary>项目：所有工作项与迭代的归属容器。</summary>
public class Project : Entity
{
    /// <summary>项目短标识，如 CP（全局唯一）。</summary>
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>绑定的 Git 仓库地址（GitHub / GitLab）。</summary>
    public string? RepoUrl { get; set; }

    /// <summary>本地工作目录（项目目录隔离 + 开发沙箱的根）。</summary>
    public string? LocalPath { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    /// <summary>默认 Agent 团队（工作流步骤未显式指定 Agent 时优先从该团队按岗位选择）。</summary>
    public Guid? DefaultTeamId { get; set; }

    /// <summary>需求类型默认团队（优先于项目默认团队）。</summary>
    public Guid? RequirementTeamId { get; set; }

    /// <summary>缺陷类型默认团队（优先于项目默认团队）。</summary>
    public Guid? DefectTeamId { get; set; }

    /// <summary>任务类型默认团队（优先于项目默认团队）。</summary>
    public Guid? TaskTeamId { get; set; }

    /// <summary>直接发布是否需要审批（开启后仅允许通过发布工作流审批发布）。</summary>
    public bool ReleaseRequiresApproval { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();

    public ICollection<WorkItems.WorkItem> WorkItems { get; set; } = new List<WorkItems.WorkItem>();

    public ICollection<Iterations.Iteration> Iterations { get; set; } = new List<Iterations.Iteration>();

    public ICollection<Iterations.Milestone> Milestones { get; set; } = new List<Iterations.Milestone>();

    public ICollection<Prototypes.Prototype> Prototypes { get; set; } = new List<Prototypes.Prototype>();
}
