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
    string? LocalPath,
    string Status,
    int WorkItemCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? DefaultTeamId,
    Guid? RequirementTeamId,
    Guid? DefectTeamId,
    Guid? TaskTeamId);

public sealed record CreateProjectRequest(
    string Key,
    string Name,
    string? Description,
    string? RepoUrl,
    string? LocalPath = null);

public sealed record UpdateProjectRequest(
    string Name,
    string? Description,
    string? RepoUrl,
    string? LocalPath = null);

public sealed record ProjectMemberDto(
    Guid Id,
    Guid ProjectId,
    string Kind,
    string Role,
    string DisplayName,
    DateTimeOffset CreatedAt,
    decimal? HourlyRate);

public sealed record AddProjectMemberRequest(
    string? DisplayName,
    string? Role,
    Guid? AgentId = null,
    decimal? HourlyRate = null);

public sealed record UpdateProjectMemberRequest(
    string DisplayName,
    string Role,
    decimal? HourlyRate = null);

public sealed record SetProjectTeamRequest(Guid? TeamId);

/// <summary>按工作项类型设置默认团队（空表示回退到项目默认团队）。</summary>
public sealed record SetProjectTypeTeamsRequest(
    Guid? RequirementTeamId,
    Guid? DefectTeamId,
    Guid? TaskTeamId);
