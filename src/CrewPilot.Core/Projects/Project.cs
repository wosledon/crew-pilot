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

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();

    public ICollection<WorkItems.WorkItem> WorkItems { get; set; } = new List<WorkItems.WorkItem>();

    public ICollection<Iterations.Iteration> Iterations { get; set; } = new List<Iterations.Iteration>();

    public ICollection<Iterations.Milestone> Milestones { get; set; } = new List<Iterations.Milestone>();

    public ICollection<Prototypes.Prototype> Prototypes { get; set; } = new List<Prototypes.Prototype>();
}
