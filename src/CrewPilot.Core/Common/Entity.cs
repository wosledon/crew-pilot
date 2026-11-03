namespace CrewPilot.Core.Common;

/// <summary>实体基类。</summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
