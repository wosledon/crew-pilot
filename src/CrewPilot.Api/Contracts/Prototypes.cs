namespace CrewPilot.Api.Contracts;

public sealed record PrototypeFileDto(
    Guid Id,
    Guid PrototypeId,
    string Path,
    string Content,
    DateTimeOffset UpdatedAt);

public sealed record UpsertPrototypeFileRequest(string Path, string Content);

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

public sealed record PrototypeVersionDto(
    Guid Id,
    Guid PrototypeId,
    int Version,
    string Name,
    string Content,
    string? CreatedByName,
    DateTimeOffset CreatedAt);

public sealed record PrototypeImageDto(string Path, string Url, long Size);

public sealed record CreatePrototypeRequest(
    string Name,
    string Type,
    string Content,
    Guid? RequirementId);

public sealed record UpdatePrototypeRequest(
    string Name,
    string Content,
    Guid? RequirementId = null);
