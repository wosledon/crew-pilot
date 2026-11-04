namespace CrewPilot.Core.Prototypes;

using CrewPilot.Core.Common;

/// <summary>原型多文件：HTML 原型的样式/脚本/页面片段，路径相对原型根目录。</summary>
public class PrototypeFile : Entity
{
    public Guid PrototypeId { get; set; }

    public Prototype Prototype { get; set; } = null!;

    /// <summary>相对路径，如 index.html / styles.css / pages/about.html。</summary>
    public string Path { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
