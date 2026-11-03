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
