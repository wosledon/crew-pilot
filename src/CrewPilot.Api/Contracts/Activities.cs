namespace CrewPilot.Api.Contracts;

public sealed record ActivityDto(
    Guid Id,
    string EventType,
    string Payload,
    string? ActorName,
    string? ActorKind,
    Guid? WorkItemId,
    DateTimeOffset CreatedAt);
