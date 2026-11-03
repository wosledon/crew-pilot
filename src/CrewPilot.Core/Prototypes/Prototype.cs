namespace CrewPilot.Core.Prototypes;

using CrewPilot.Core.Common;

/// <summary>原型类型：来源不限，HTML / URL / 图片 / 设计 MCP 引用一律一等公民。</summary>
public enum PrototypeType
{
    Html,
    Url,
    Image,
    Mcp,
}

/// <summary>原型：HTML 在原型工坊编写并直接运行，也可引用外部设计源。</summary>
public class Prototype : Entity
{
    public Guid ProjectId { get; set; }

    public Projects.Project Project { get; set; } = null!;

    public Guid? RequirementId { get; set; }

    public WorkItems.Requirement? Requirement { get; set; }

    public string Name { get; set; } = string.Empty;

    public PrototypeType Type { get; set; }

    /// <summary>内容：HTML 源码 / URL / 图片存储路径 / MCP 引用地址。</summary>
    public string Content { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    public string? ThumbnailPath { get; set; }

    public Projects.MemberKind CreatedByKind { get; set; } = Projects.MemberKind.User;

    public string? CreatedByName { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
