namespace CrewPilot.Api.Contracts;

public sealed record MilestoneDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description,
    DateOnly Date,
    int IterationCount);

public sealed record CreateMilestoneRequest(string Name, string? Description, DateOnly Date);

public sealed record UpdateMilestoneRequest(string Name, string? Description, DateOnly Date);
