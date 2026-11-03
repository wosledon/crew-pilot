namespace CrewPilot.Api.Contracts;

public sealed record AgentListItemDto(
    Guid Id,
    string Name,
    string Job,
    string ProviderName,
    string Model,
    bool Enabled,
    int RunCount);

public sealed record AgentDetailDto(
    Guid Id,
    string Name,
    string Job,
    Guid ProviderId,
    string ProviderName,
    string Model,
    string SystemPrompt,
    string? Tools,
    string? CommandWhitelist,
    bool Enabled);

public sealed record CreateAgentRequest(
    string Name,
    string Job,
    Guid ProviderId,
    string Model,
    string SystemPrompt,
    string? Tools,
    string? CommandWhitelist);

public sealed record UpdateAgentRequest(
    string Name,
    string Job,
    string SystemPrompt,
    string? Tools,
    string? CommandWhitelist,
    bool Enabled);

public sealed record AgentRunListItemDto(
    Guid Id,
    string AgentName,
    Guid? WorkItemId,
    string? WorkItemTitle,
    string Trigger,
    string Status,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt,
    TimeSpan? Duration,
    int? PromptTokens,
    int? CompletionTokens);

public sealed record RunLogDto(int Sequence, string Kind, string Content, DateTimeOffset CreatedAt);

public sealed record AgentRunDetailDto(
    Guid Id,
    string AgentName,
    string? Model,
    Guid? WorkItemId,
    string? WorkItemTitle,
    string Trigger,
    string Status,
    string? WorkingDirectory,
    string Prompt,
    string? Output,
    string? Error,
    int? PromptTokens,
    int? CompletionTokens,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    TimeSpan? Duration,
    IReadOnlyList<RunLogDto> Logs);

public sealed record AssignAgentRequest(Guid AgentId);

public sealed record PreviewAgentRequest(string Prompt);
