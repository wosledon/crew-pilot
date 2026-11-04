namespace CrewPilot.Api.Contracts;

public sealed record WorkflowStepDto(
    Guid Id,
    int Order,
    string Name,
    Guid? AgentId,
    string? AgentName,
    string? RequiredJob,
    string? Instruction,
    string ApprovalPolicy,
    int MaxRetries,
    string? InputContract,
    string? OutputContract);

public sealed record WorkflowDto(
    Guid Id,
    Guid? ProjectId,
    string Name,
    string? Description,
    bool IsTemplate,
    bool Enabled,
    IReadOnlyList<WorkflowStepDto> Steps,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record WorkflowStepRequest(
    string Name,
    int Order,
    Guid? AgentId,
    string? RequiredJob,
    string? Instruction,
    string? ApprovalPolicy,
    int? MaxRetries,
    string? InputContract,
    string? OutputContract);

public sealed record CreateWorkflowRequest(
    string Name,
    string? Description,
    IReadOnlyList<WorkflowStepRequest> Steps);

public sealed record UpdateWorkflowRequest(
    string Name,
    string? Description,
    IReadOnlyList<WorkflowStepRequest> Steps);

public sealed record CreateWorkflowFromTemplateRequest(string TemplateKey, string? Name);

public sealed record WorkflowTemplateStepDto(
    string Name,
    string Job,
    string? Instruction,
    string ApprovalPolicy,
    int MaxRetries);

public sealed record WorkflowTemplateDto(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<WorkflowTemplateStepDto> Steps);

public sealed record StartWorkflowRequest(Guid WorkItemId);

public sealed record WorkflowRunDto(
    Guid Id,
    Guid WorkflowId,
    string WorkflowName,
    Guid WorkItemId,
    string WorkItemTitle,
    string Status,
    int CurrentStepOrder,
    Guid? CurrentStepId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt);

public sealed record WorkflowRunDetailDto(
    WorkflowRunDto Run,
    IReadOnlyList<AgentRunListItemDto> AgentRuns,
    IReadOnlyList<BlockerDto> Blockers);

public sealed record BlockerDto(
    Guid Id,
    Guid ProjectId,
    Guid WorkItemId,
    string WorkItemTitle,
    Guid? WorkflowRunId,
    Guid? AgentRunId,
    string Kind,
    string Reason,
    string? ContextSnapshot,
    string Status,
    string? Resolution,
    string? ResolutionNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);

public sealed record ResolveBlockerRequest(string Resolution, string? Note);

public sealed record AgentTeamDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Goal,
    IReadOnlyList<AgentTeamMemberDto> Members);

public sealed record AgentTeamMemberDto(Guid AgentId, string AgentName, string? Role);

public sealed record CreateAgentTeamRequest(
    string Name,
    string? Goal,
    IReadOnlyList<Guid> AgentIds);
