namespace CrewPilot.Api.Contracts;

public sealed record PrototypeDto(
    Guid Id,
    Guid ProjectId,
    Guid? RequirementId,
    string Name,
    string Type,
    string Content,
    int Version,
    string? CreatedByName,
    DateTimeOffset UpdatedAt);

public sealed record CreatePrototypeRequest(string Name, string Type, string Content, Guid? RequirementId);

public sealed record UpdatePrototypeRequest(string Name, string Content);
