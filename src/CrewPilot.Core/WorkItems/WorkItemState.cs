namespace CrewPilot.Core.WorkItems;

/// <summary>优先级。</summary>
public enum Priority
{
    P0,
    P1,
    P2,
    P3,
}

/// <summary>需求状态：待梳理 → 已就绪 → 进行中 → 待评审 → 已完成。</summary>
public enum RequirementStatus
{
    Backlog,
    Ready,
    InProgress,
    InReview,
    Done,
    Rejected,
}

/// <summary>缺陷状态：新建 → 已确认 → 修复中 → 待验证 → 已关闭。</summary>
public enum DefectStatus
{
    New,
    Confirmed,
    Fixing,
    PendingVerification,
    Closed,
    NotADefect,
    Deferred,
}

/// <summary>严重级别。</summary>
public enum Severity
{
    Fatal,
    Serious,
    Normal,
    Minor,
}
