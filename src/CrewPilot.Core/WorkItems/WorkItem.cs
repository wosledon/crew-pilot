namespace CrewPilot.Core.WorkItems;

using System.ComponentModel.DataAnnotations.Schema;
using CrewPilot.Core.Collaboration;
using CrewPilot.Core.Common;
using CrewPilot.Core.Iterations;
using CrewPilot.Core.Projects;
using CrewPilot.Core.WorkLogs;

/// <summary>
/// 工作项统一基类（TPH 继承，鉴别列 WorkItemType）。
/// 含工时三字段：预估 / 已耗 / 剩余。
/// </summary>
public abstract class WorkItem : Entity
{
    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>验收标准（checklist 文本）。</summary>
    public string? AcceptanceCriteria { get; set; }

    /// <summary>标签（逗号分隔，如 "前端,体验"）。</summary>
    public string? Tags { get; set; }

    public Priority Priority { get; set; } = Priority.P2;

    public int? StoryPoints { get; set; }

    /// <summary>预估工时（小时）。</summary>
    public decimal? EstimatedHours { get; set; }

    /// <summary>剩余工时（小时）。</summary>
    public decimal? RemainingHours { get; set; }

    public Guid? IterationId { get; set; }

    public Iteration? Iteration { get; set; }

    /// <summary>指派人：Agent 或用户本人。</summary>
    public Guid? AssigneeId { get; set; }

    public ProjectMember? Assignee { get; set; }

    public string? BranchUrl { get; set; }

    public string? PrUrl { get; set; }

    /// <summary>PR/MR 状态（open/merged/closed，CI/CD 同步回填）。</summary>
    public string? PrState { get; set; }

    /// <summary>CI 状态（success/failure/pending，CI/CD 同步回填）。</summary>
    public string? CiStatus { get; set; }

    /// <summary>截止日期（甘特图时间线与逾期标红）。</summary>
    public DateOnly? DueDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<WorkLog> WorkLogs { get; set; } = new List<WorkLog>();

    public ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();

    public ICollection<Activity> Activities { get; set; } = new List<Activity>();

    /// <summary>已耗工时：由工时登记汇总（不入库，查询时按需 Include 或以子查询投影）。</summary>
    [NotMapped]
    public decimal LoggedHours => WorkLogs.Sum(w => w.Hours);
}
