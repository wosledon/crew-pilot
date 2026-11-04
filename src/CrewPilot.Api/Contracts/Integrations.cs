namespace CrewPilot.Api.Contracts;

public sealed record ProjectIntegrationDto(
    Guid Id,
    Guid ProjectId,
    string Platform,
    bool HasToken,
    string? Repository,
    DateTimeOffset UpdatedAt);

public sealed record UpsertProjectIntegrationRequest(
    string Platform,
    string? Token,
    string? Repository);

public sealed record TestIntegrationResponse(bool Ok, string Message);

public sealed record CiCheckDto(
    string Name,
    string Status,
    string? Conclusion,
    string? Url);

public sealed record PullRequestStatusDto(
    string State,
    string CiStatus,
    bool? Mergeable,
    string? Url,
    IReadOnlyList<CiCheckDto> Checks);

public sealed record CreatePullRequestRequest(
    string Branch,
    string Title,
    string? Body);

public sealed record PullRequestCreatedDto(string Url);

public sealed record CreateReleaseRequest(string Tag, string? Title, string? Notes);

public sealed record ReleaseDto(string Tag, string? Url, string Message);

/// <summary>CI/CD 平台 Webhook 推送（PR 状态 / CI 状态）。</summary>
public sealed record IntegrationWebhookRequest(
    string? PrUrl,
    string? State,
    string? CiStatus);
