namespace CrewPilot.Core.Prototypes;

using CrewPilot.Core.Common;

/// <summary>原型版本快照：每次保存自动留档，可查看与恢复。</summary>
public class PrototypeVersion : Entity
{
    public Guid PrototypeId { get; set; }

    public Prototype Prototype { get; set; } = null!;

    public int Version { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? CreatedByName { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
