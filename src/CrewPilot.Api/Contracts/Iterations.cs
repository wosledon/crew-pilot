namespace CrewPilot.Api.Contracts;

public sealed record IterationListItemDto(
    Guid Id,
    string Name,
    string? Goal,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    int WorkItemCount,
    decimal EstimatedHours,
    decimal LoggedHours,
    Guid? MilestoneId);

public sealed record IterationDetailDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Goal,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    int WorkItemCount,
    Guid? MilestoneId);

public sealed record CreateIterationRequest(
    string Name,
    string? Goal,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid? MilestoneId = null);

public sealed record UpdateIterationRequest(
    string Name,
    string? Goal,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid? MilestoneId = null);

/// <summary>燃尽图数据点：某日的剩余工时与理想剩余工时。</summary>
public sealed record BurndownPointDto(DateOnly Date, decimal RemainingHours, decimal IdealHours);

public sealed record BurndownDto(
    Guid IterationId,
    decimal TotalEstimatedHours,
    IReadOnlyList<BurndownPointDto> Points);

/// <summary>工时报表：按维度分组的预估/已耗工时。</summary>
public sealed record HoursByGroupDto(string Key, string Label, decimal EstimatedHours, decimal LoggedHours);

public sealed record HoursReportDto(
    decimal TotalEstimatedHours,
    decimal TotalLoggedHours,
    IReadOnlyList<HoursByGroupDto> ByType,
    IReadOnlyList<HoursByGroupDto> ByAssignee,
    IReadOnlyList<HoursByGroupDto> ByMember);

/// <summary>人力成本：工时 × 成员时薪。</summary>
public sealed record HumanCostByMemberDto(
    string Key,
    string Label,
    decimal Hours,
    decimal? HourlyRate,
    decimal Cost);

public sealed record HumanCostReportDto(
    decimal TotalHours,
    decimal TotalCost,
    IReadOnlyList<HumanCostByMemberDto> ByMember);

/// <summary>估算偏差：已耗 vs 预估（按类型）。</summary>
public sealed record EstimateDeviationRowDto(
    string Key,
    string Label,
    decimal EstimatedHours,
    decimal LoggedHours,
    decimal DeviationHours,
    decimal? DeviationPercent,
    int ItemCount,
    int OverrunCount);

public sealed record EstimateDeviationReportDto(
    decimal EstimatedHours,
    decimal LoggedHours,
    decimal DeviationHours,
    decimal? DeviationPercent,
    int ItemCount,
    int OverrunCount,
    IReadOnlyList<EstimateDeviationRowDto> ByType);

/// <summary>项目周报（数据汇总，可直接展示或交给 PM Agent 生成文字版）。</summary>
public sealed record WeeklyReportDto(
    DateOnly From,
    DateOnly To,
    string Markdown,
    int CompletedCount,
    int ActiveCount,
    int OpenBlockers);
