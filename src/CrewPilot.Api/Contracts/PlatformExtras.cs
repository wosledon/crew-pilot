namespace CrewPilot.Api.Contracts;

public sealed record SkillDto(
    Guid Id,
    string Name,
    string? Description,
    string PromptContent,
    string? Tools,
    string? ScriptPath,
    bool Enabled,
    int AgentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateSkillRequest(
    string Name,
    string? Description,
    string PromptContent,
    string? Tools,
    string? ScriptPath);

public sealed record UpdateSkillRequest(
    string Name,
    string? Description,
    string PromptContent,
    string? Tools,
    string? ScriptPath,
    bool Enabled);

/// <summary>SKILL.md 导入：YAML front matter + 正文提示词。</summary>
public sealed record ImportSkillRequest(string Content);

public sealed record McpServerDto(
    Guid Id,
    string Name,
    string Endpoint,
    string Transport,
    bool HasToken,
    bool Enabled,
    int AgentCount,
    DateTimeOffset UpdatedAt);

public sealed record CreateMcpServerRequest(
    string Name,
    string Endpoint,
    string Transport,
    string? AuthToken);

public sealed record UpdateMcpServerRequest(
    string Name,
    string Endpoint,
    string Transport,
    string? AuthToken,
    bool Enabled);

public sealed record McpTestDto(bool Ok, string Message);

public sealed record AuditLogDto(
    Guid Id,
    Guid? ProjectId,
    string Action,
    string TargetType,
    string? TargetId,
    string Actor,
    string? Detail,
    DateTimeOffset CreatedAt);

public sealed record CostByModelDto(
    string Provider,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    decimal Cost);

public sealed record CostReportDto(
    int TotalPromptTokens,
    int TotalCompletionTokens,
    decimal TotalCost,
    IReadOnlyList<CostByModelDto> ByModel);
