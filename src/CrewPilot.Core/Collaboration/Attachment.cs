namespace CrewPilot.Core.Collaboration;

using CrewPilot.Core.Common;

/// <summary>附件（测试报告、日志等）。</summary>
public class Attachment : Entity
{
    public Guid WorkItemId { get; set; }

    public WorkItems.WorkItem WorkItem { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long Size { get; set; }

    /// <summary>存储路径（相对数据目录）。</summary>
    public string StoragePath { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
