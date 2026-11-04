namespace CrewPilot.Api.Contracts;

public sealed record WorkItemListItemDto(
    Guid Id,
    string Type,
    string Title,
    string Status,
    string Priority,
    string? AssigneeName,
    decimal? EstimatedHours,
    decimal LoggedHours,
    decimal? RemainingHours,
    DateOnly? DueDate,
    string? PrUrl,
    DateTimeOffset UpdatedAt,
    string? Tags);

public sealed record WorkItemDetailDto(
    Guid Id,
    string Type,
    string Title,
    string Status,
    string Priority,
    string? Description,
    string? AcceptanceCriteria,
    string? Severity,
    string? ReproSteps,
    string? Environment,
    int? StoryPoints,
    decimal? EstimatedHours,
    decimal LoggedHours,
    decimal? RemainingHours,
    string? AssigneeName,
    Guid? IterationId,
    DateOnly? DueDate,
    string? BranchUrl,
    string? PrUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? Tags,
    Guid? ParentWorkItemId);

public sealed record CreateWorkItemRequest(
    string Type,
    string Title,
    string? Description,
    string? AcceptanceCriteria,
    string? Priority,
    int? StoryPoints,
    decimal? EstimatedHours,
    decimal? RemainingHours,
    DateOnly? DueDate,
    Guid? IterationId,
    string? AssigneeName,
    string? Severity,
    string? ReproSteps,
    string? Environment,
    string? Tags = null,
    Guid? ParentWorkItemId = null);

public sealed record UpdateWorkItemRequest(
    string Title,
    string? Description,
    string? AcceptanceCriteria,
    string? Priority,
    int? StoryPoints,
    decimal? EstimatedHours,
    decimal? RemainingHours,
    DateOnly? DueDate,
    Guid? IterationId,
    string? AssigneeName = null,
    string? Severity = null,
    string? ReproSteps = null,
    string? Environment = null,
    string? Tags = null,
    Guid? ParentWorkItemId = null,
    string? BranchUrl = null,
    string? PrUrl = null);

public sealed record TransitionRequest(string ToStatus);

public sealed record WorkLogDto(
    Guid Id,
    decimal Hours,
    DateOnly Date,
    string? Note,
    string? MemberName,
    Guid? AgentRunId,
    DateTimeOffset CreatedAt);

public sealed record CreateWorkLogRequest(decimal Hours, DateOnly? Date, string? Note);
