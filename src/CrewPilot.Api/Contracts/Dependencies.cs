namespace CrewPilot.Api.Contracts;

public sealed record WorkItemDependencyDto(
    Guid Id,
    Guid ProjectId,
    Guid FromWorkItemId,
    string FromTitle,
    Guid ToWorkItemId,
    string ToTitle,
    string Type,
    DateTimeOffset CreatedAt);

public sealed record CreateWorkItemDependencyRequest(
    Guid FromWorkItemId,
    Guid ToWorkItemId,
    string? Type);

/// <summary>关键路径：按预估工时加权的最长依赖链。</summary>
public sealed record CriticalPathDto(decimal TotalHours, IReadOnlyList<Guid> ItemIds);
