namespace CrewPilot.Api.Contracts;

public sealed record IterationListItemDto(
    Guid Id,
    string Name,
    string? Goal,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    int WorkItemCount,
    decimal EstimatedHours,
    decimal LoggedHours);

public sealed record IterationDetailDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Goal,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    int WorkItemCount);

public sealed record CreateIterationRequest(string Name, string? Goal, DateOnly StartDate, DateOnly EndDate);

public sealed record UpdateIterationRequest(string Name, string? Goal, DateOnly StartDate, DateOnly EndDate);

/// <summary>燃尽图数据点：某日的剩余工时与理想剩余工时。</summary>
public sealed record BurndownPointDto(DateOnly Date, decimal RemainingHours, decimal IdealHours);

public sealed record BurndownDto(
    Guid IterationId,
    decimal TotalEstimatedHours,
    IReadOnlyList<BurndownPointDto> Points);

/// <summary>工时报表：按维度分组的预估/已耗工时。</summary>
public sealed record HoursByGroupDto(string Key, string Label, decimal EstimatedHours, decimal LoggedHours);

public sealed record HoursReportDto(
    decimal TotalEstimatedHours,
    decimal TotalLoggedHours,
    IReadOnlyList<HoursByGroupDto> ByType,
    IReadOnlyList<HoursByGroupDto> ByAssignee);
