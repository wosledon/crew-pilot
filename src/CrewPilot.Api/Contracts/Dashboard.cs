namespace CrewPilot.Api.Contracts;

/// <summary>
/// 驾驶舱聚合数据：一个仪表盘承载项目进度、全局卡点队列、我的待办、
/// 进行中的 Agent 执行与最近活动（PRD 4.1 / 5.8）。
/// </summary>
public sealed record DashboardDto(
    DashboardStatsDto Stats,
    IReadOnlyList<DashboardProjectDto> Projects,
    IReadOnlyList<DashboardBlockerDto> Blockers,
    IReadOnlyList<DashboardTodoDto> Todos,
    IReadOnlyList<DashboardRunDto> ActiveRuns,
    IReadOnlyList<DashboardActivityDto> RecentActivities);

public sealed record DashboardStatsDto(
    int ProjectCount,
    int OpenBlockerCount,
    int ActiveRunCount,
    int TodoCount,
    decimal WeekLoggedHours);

public sealed record DashboardProjectDto(
    Guid Id,
    string Key,
    string Name,
    string Status,
    int WorkItemCount,
    int DoneCount,
    double Progress,
    string? ActiveIterationName,
    DateOnly? ActiveIterationEndDate);

public sealed record DashboardBlockerDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    Guid WorkItemId,
    string WorkItemTitle,
    string Kind,
    string Reason,
    string? ContextSnapshot,
    DateTimeOffset CreatedAt);

public sealed record DashboardTodoDto(
    Guid WorkItemId,
    Guid ProjectId,
    string ProjectName,
    string Title,
    string Type,
    string Status,
    string Priority,
    DateOnly? DueDate,
    decimal? RemainingHours,
    bool Overdue);

public sealed record DashboardRunDto(
    Guid Id,
    string AgentName,
    Guid? ProjectId,
    string? ProjectName,
    string? WorkItemTitle,
    string Status,
    string Trigger,
    DateTimeOffset CreatedAt);

public sealed record DashboardActivityDto(
    Guid Id,
    Guid ProjectId,
    string? ProjectName,
    string EventType,
    string Payload,
    string? ActorName,
    string? ActorKind,
    Guid? WorkItemId,
    DateTimeOffset CreatedAt);
