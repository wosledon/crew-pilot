namespace CrewPilot.Api.Contracts;

public sealed record ProjectSummaryDto(
    Guid Id,
    string Key,
    string Name,
    string? RepoUrl,
    string Status,
    int WorkItemCount,
    DateTimeOffset CreatedAt);

public sealed record ProjectDetailDto(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    string? RepoUrl,
    string Status,
    int WorkItemCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateProjectRequest(string Key, string Name, string? Description, string? RepoUrl);

public sealed record UpdateProjectRequest(string Name, string? Description, string? RepoUrl);
