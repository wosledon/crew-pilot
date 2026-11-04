/**
 * 前端类型：API 响应/请求优先取自 OpenAPI 生成契约（`api-schema.d.ts`），
 * 仅保留领域联合类型（状态、类型枚举）与前端专用结构。
 * 新增后端 DTO 后运行 `npm run api:types` 同步生成。
 */
import type { components } from "./api-schema";

type Api = components["schemas"];

export type ProjectStatus = "active" | "archived";

/** 单用户认证状态 */
export type AuthStatus = Api["AuthStatusDto"];

export type ProjectSummary = Omit<Api["ProjectSummaryDto"], "status"> & {
  status: ProjectStatus;
};

export type ProjectDetail = Omit<Api["ProjectDetailDto"], "status"> & {
  status: ProjectStatus;
};

export type WorkItemType = "requirement" | "defect" | "task";

export type WorkItemListItem = Omit<Api["WorkItemListItemDto"], "type"> & {
  type: WorkItemType;
};

export type WorkItemDetail = Omit<Api["WorkItemDetailDto"], "type"> & {
  type: WorkItemType;
};

/** 批量操作结果 */
export type BatchItemResult = Api["BatchItemResultDto"];

export type BatchWorkItemsResult = Api["BatchWorkItemsResultDto"];

export type WorkLog = Api["WorkLogDto"];

/** 工作项依赖（甘特图 FS/SS） */
export type DependencyType = "finishtostart" | "starttostart";

export type WorkItemDependency = Omit<Api["WorkItemDependencyDto"], "type"> & {
  type: DependencyType;
};

export type CriticalPath = Api["CriticalPathDto"];

/** 项目周报（PM Agent / 数据汇总） */
export type WeeklyReport = Api["WeeklyReportDto"];

export type PrototypeType = "html" | "url" | "image" | "mcp";

export type Prototype = Omit<Api["PrototypeDto"], "type"> & {
  type: PrototypeType;
};

export type PrototypeVersion = Api["PrototypeVersionDto"];

/** 原型生成任务（产品 Agent 产出 HTML） */
export type PrototypeGeneration = Api["PrototypeGenerationDto"];

export type PrototypeImage = Api["PrototypeImageDto"];

/** 原型多文件（文本资源） */
export type PrototypeFile = Api["PrototypeFileDto"];

export type IterationStatus = "planned" | "active" | "completed";

export type IterationListItem = Omit<Api["IterationListItemDto"], "status"> & {
  status: IterationStatus;
};

export type IterationDetail = Omit<Api["IterationDetailDto"], "status"> & {
  status: IterationStatus;
};

export type Milestone = Api["MilestoneDto"];

export type CommentAuthorKind = "user" | "agent";

export type Comment = Omit<Api["CommentDto"], "authorKind"> & {
  authorKind: CommentAuthorKind;
};

export type Attachment = Api["AttachmentDto"];

export type BurndownPoint = Api["BurndownPointDto"];

export type Burndown = Api["BurndownDto"];

export type HoursByGroup = Api["HoursByGroupDto"];

export type HoursReport = Api["HoursReportDto"];

/** 人力成本报表 */
export type HumanCostByMember = Api["HumanCostByMemberDto"];

export type HumanCostReport = Api["HumanCostReportDto"];

/** 估算偏差报表 */
export type EstimateDeviationRow = Api["EstimateDeviationRowDto"];

export type EstimateDeviationReport = Api["EstimateDeviationReportDto"];

export type ProjectMember = Omit<Api["ProjectMemberDto"], "kind"> & {
  kind: "user" | "agent";
};

export type Activity = Api["ActivityDto"];

export type ProviderListItem = Api["ProviderListItemDto"];

export type ModelDto = Api["ModelDto"];

export type ProviderDetail = Api["ProviderDetailDto"];

export type AgentListItem = Api["AgentListItemDto"];

export type AgentDetail = Api["AgentDetailDto"];

export type AgentRunStatus =
  "queued" | "running" | "succeeded" | "failed" | "canceled" | "waitingforhuman";

export type AgentRunListItem = Omit<Api["AgentRunListItemDto"], "status"> & {
  status: AgentRunStatus;
};

export type AgentRunLog = Api["RunLogDto"];

export type AgentRunDetail = Omit<Api["AgentRunDetailDto"], "status"> & {
  status: AgentRunStatus;
};

/** 工作流（M3） */
export type ApprovalPolicy = "auto" | "blockersonly" | "always";
export type WorkflowRunStatus = "running" | "waitingforhuman" | "succeeded" | "failed" | "canceled";
export type BlockerKind = "agentblocked" | "approval" | "failure";
export type BlockerStatus = "open" | "resolved";
export type BlockerResolution = "continue" | "redirect" | "cancel";

export type WorkflowStep = Omit<Api["WorkflowStepDto"], "approvalPolicy"> & {
  approvalPolicy: ApprovalPolicy;
};

export type Workflow = Omit<Api["WorkflowDto"], "steps"> & {
  steps: WorkflowStep[];
};

export type WorkflowTemplateStep = Omit<Api["WorkflowTemplateStepDto"], "approvalPolicy"> & {
  approvalPolicy: ApprovalPolicy;
};

export type WorkflowTemplate = Omit<Api["WorkflowTemplateDto"], "steps"> & {
  steps: WorkflowTemplateStep[];
};

export type WorkflowRun = Omit<Api["WorkflowRunDto"], "status"> & {
  status: WorkflowRunStatus;
};

export type Blocker = Omit<Api["BlockerDto"], "kind" | "status" | "resolution"> & {
  kind: BlockerKind;
  status: BlockerStatus;
  resolution: BlockerResolution | null;
};

export interface WorkflowRunDetail {
  run: WorkflowRun;
  agentRuns: AgentRunListItem[];
  blockers: Blocker[];
}

export type AgentTeamMember = Api["AgentTeamMemberDto"];

export type AgentTeam = Api["AgentTeamDto"];

/** CI/CD（M4） */
export type ProjectIntegration = Omit<Api["ProjectIntegrationDto"], "platform"> & {
  platform: "github" | "gitlab";
};

export type CiCheck = Api["CiCheckDto"];

export type PullRequestState = "open" | "merged" | "closed" | "unknown";
export type CiStatus = "success" | "failure" | "pending" | "none";

export type PullRequestStatus = Omit<Api["PullRequestStatusDto"], "state" | "ciStatus"> & {
  state: PullRequestState;
  ciStatus: CiStatus;
};

export type ReleaseResult = Api["ReleaseDto"];

/** 工作项最近一次 Agent 活动（看板实时状态） */
export type WorkItemAgentActivity = Omit<Api["WorkItemAgentActivityDto"], "status"> & {
  status: AgentRunStatus;
};

/** 驾驶舱聚合视图（PRD 4.1 / 5.8） */
export type DashboardStats = Api["DashboardStatsDto"];

export type DashboardProject = Omit<Api["DashboardProjectDto"], "status"> & {
  status: ProjectStatus;
};

export type DashboardBlocker = Omit<Api["DashboardBlockerDto"], "kind"> & {
  kind: BlockerKind;
};

export type DashboardTodo = Omit<Api["DashboardTodoDto"], "type"> & {
  type: WorkItemType;
};

export type DashboardRun = Omit<Api["DashboardRunDto"], "status"> & {
  status: AgentRunStatus;
};

export type DashboardActivity = Omit<Api["DashboardActivityDto"], "actorKind"> & {
  actorKind: "user" | "agent" | null;
};

export interface Dashboard {
  stats: DashboardStats;
  projects: DashboardProject[];
  blockers: DashboardBlocker[];
  todos: DashboardTodo[];
  activeRuns: DashboardRun[];
  recentActivities: DashboardActivity[];
}

/** M5：Skills / MCP / 审计 / 成本 */
export type Skill = Api["SkillDto"];

export type McpServer = Api["McpServerDto"];

export type AuditLogEntry = Api["AuditLogDto"];

export type CostByModel = Api["CostByModelDto"];

export type CostReport = Api["CostReportDto"];

/** API 错误（携带 ProblemDetails 状态码） */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}
