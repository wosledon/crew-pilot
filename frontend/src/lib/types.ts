/** 与后端 DTO 对齐的前端类型（后续切换为 OpenAPI 生成客户端） */

export type ProjectStatus = "active" | "archived";

/** 单用户认证状态 */
export interface AuthStatus {
  enabled: boolean;
  authenticated: boolean;
}

export interface ProjectSummary {
  id: string;
  key: string;
  name: string;
  repoUrl: string | null;
  status: ProjectStatus;
  workItemCount: number;
  createdAt: string;
}

export interface ProjectDetail {
  id: string;
  key: string;
  name: string;
  description: string | null;
  repoUrl: string | null;
  localPath: string | null;
  status: ProjectStatus;
  workItemCount: number;
  createdAt: string;
  updatedAt: string;
  defaultTeamId: string | null;
}

export type WorkItemType = "requirement" | "defect" | "task";

export interface WorkItemListItem {
  id: string;
  type: WorkItemType;
  title: string;
  status: string;
  priority: string;
  assigneeName: string | null;
  estimatedHours: number | null;
  loggedHours: number;
  remainingHours: number | null;
  dueDate: string | null;
  prUrl: string | null;
  updatedAt: string;
  tags: string | null;
}

export interface WorkItemDetail {
  id: string;
  type: WorkItemType;
  title: string;
  status: string;
  priority: string;
  description: string | null;
  acceptanceCriteria: string | null;
  severity: string | null;
  reproSteps: string | null;
  environment: string | null;
  storyPoints: number | null;
  estimatedHours: number | null;
  loggedHours: number;
  remainingHours: number | null;
  assigneeName: string | null;
  iterationId: string | null;
  dueDate: string | null;
  branchUrl: string | null;
  prUrl: string | null;
  createdAt: string;
  updatedAt: string;
  tags: string | null;
  parentWorkItemId: string | null;
  relatedRequirementId: string | null;
  relatedRequirementTitle: string | null;
}

/** 批量操作结果 */
export interface BatchItemResult {
  id: string;
  ok: boolean;
  error: string | null;
}

export interface BatchWorkItemsResult {
  succeeded: number;
  failed: number;
  results: BatchItemResult[];
}

export interface WorkLog {
  id: string;
  hours: number;
  date: string;
  note: string | null;
  memberName: string | null;
  createdAt: string;
}

export type PrototypeType = "html" | "url" | "image" | "mcp";
export interface Prototype {
  id: string;
  projectId: string;
  requirementId: string | null;
  name: string;
  type: PrototypeType;
  content: string;
  version: number;
  createdByName: string | null;
  updatedAt: string;
}

export interface PrototypeVersion {
  id: string;
  prototypeId: string;
  version: number;
  name: string;
  content: string;
  createdByName: string | null;
  createdAt: string;
}

export interface PrototypeImage {
  path: string;
  url: string;
  size: number;
}

export type IterationStatus = "planned" | "active" | "completed";

export interface IterationListItem {
  id: string;
  name: string;
  goal: string | null;
  startDate: string;
  endDate: string;
  status: IterationStatus;
  workItemCount: number;
  estimatedHours: number;
  loggedHours: number;
  milestoneId: string | null;
}

export interface IterationDetail {
  id: string;
  projectId: string;
  name: string;
  goal: string | null;
  startDate: string;
  endDate: string;
  status: IterationStatus;
  workItemCount: number;
  milestoneId: string | null;
}

export interface Milestone {
  id: string;
  projectId: string;
  name: string;
  description: string | null;
  date: string;
  iterationCount: number;
}

export type CommentAuthorKind = "user" | "agent";

export interface Comment {
  id: string;
  workItemId: string;
  content: string;
  authorName: string;
  authorKind: CommentAuthorKind;
  createdAt: string;
}

export interface Attachment {
  id: string;
  workItemId: string;
  fileName: string;
  contentType: string;
  size: number;
  createdAt: string;
}

export interface BurndownPoint {
  date: string;
  remainingHours: number;
  idealHours: number;
}

export interface Burndown {
  iterationId: string;
  totalEstimatedHours: number;
  points: BurndownPoint[];
}

export interface HoursByGroup {
  key: string;
  label: string;
  estimatedHours: number;
  loggedHours: number;
}

export interface HoursReport {
  totalEstimatedHours: number;
  totalLoggedHours: number;
  byType: HoursByGroup[];
  byAssignee: HoursByGroup[];
  byMember: HoursByGroup[];
}

/** 人力成本报表 */
export interface HumanCostByMember {
  key: string;
  label: string;
  hours: number;
  hourlyRate: number | null;
  cost: number;
}

export interface HumanCostReport {
  totalHours: number;
  totalCost: number;
  byMember: HumanCostByMember[];
}

/** 估算偏差报表 */
export interface EstimateDeviationRow {
  key: string;
  label: string;
  estimatedHours: number;
  loggedHours: number;
  deviationHours: number;
  deviationPercent: number | null;
  itemCount: number;
  overrunCount: number;
}

export interface EstimateDeviationReport {
  estimatedHours: number;
  loggedHours: number;
  deviationHours: number;
  deviationPercent: number | null;
  itemCount: number;
  overrunCount: number;
  byType: EstimateDeviationRow[];
}

export interface ProjectMember {
  id: string;
  projectId: string;
  kind: "user" | "agent";
  role: string;
  displayName: string;
  createdAt: string;
  hourlyRate: number | null;
}

export interface Activity {
  id: string;
  eventType: string;
  payload: string;
  actorName: string | null;
  actorKind: string | null;
  workItemId: string | null;
  createdAt: string;
}

export interface ProviderListItem {
  id: string;
  name: string;
  baseUrl: string;
  source: string;
  enabled: boolean;
  isDefault: boolean;
  modelCount: number;
}

export interface ModelDto {
  id: string;
  modelId: string;
  displayName: string;
  contextLength: number | null;
  capabilities: string | null;
  inputPricePerMillion: number | null;
  outputPricePerMillion: number | null;
  enabled: boolean;
}

export interface ProviderDetail {
  id: string;
  name: string;
  baseUrl: string;
  source: string;
  enabled: boolean;
  isDefault: boolean;
  models: ModelDto[];
}

export interface AgentListItem {
  id: string;
  name: string;
  job: string;
  providerName: string;
  model: string;
  enabled: boolean;
  runCount: number;
}

export interface AgentDetail {
  id: string;
  name: string;
  job: string;
  providerId: string;
  providerName: string;
  model: string;
  systemPrompt: string;
  tools: string | null;
  commandWhitelist: string | null;
  enabled: boolean;
  skillIds: string[];
  mcpServerIds: string[];
}

export type AgentRunStatus =
  "queued" | "running" | "succeeded" | "failed" | "canceled" | "waitingforhuman";

export interface AgentRunListItem {
  id: string;
  agentName: string;
  workItemId: string | null;
  workItemTitle: string | null;
  trigger: string;
  status: AgentRunStatus;
  error: string | null;
  createdAt: string;
  finishedAt: string | null;
  duration: string | null;
  promptTokens: number | null;
  completionTokens: number | null;
}

export interface AgentRunLog {
  sequence: number;
  kind: string;
  content: string;
  createdAt: string;
}

export interface AgentRunDetail {
  id: string;
  agentName: string;
  model: string | null;
  workItemId: string | null;
  workItemTitle: string | null;
  trigger: string;
  status: AgentRunStatus;
  workingDirectory: string | null;
  prompt: string;
  output: string | null;
  error: string | null;
  promptTokens: number | null;
  completionTokens: number | null;
  createdAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  duration: string | null;
  logs: AgentRunLog[];
}

/** 工作流（M3） */
export type ApprovalPolicy = "auto" | "blockersonly" | "always";
export type WorkflowRunStatus = "running" | "waitingforhuman" | "succeeded" | "failed" | "canceled";
export type BlockerKind = "agentblocked" | "approval" | "failure";
export type BlockerStatus = "open" | "resolved";
export type BlockerResolution = "continue" | "redirect" | "cancel";

export interface WorkflowStep {
  id: string;
  order: number;
  name: string;
  agentId: string | null;
  agentName: string | null;
  requiredJob: string | null;
  instruction: string | null;
  approvalPolicy: ApprovalPolicy;
  maxRetries: number;
  inputContract: string | null;
  outputContract: string | null;
}

export interface Workflow {
  id: string;
  projectId: string | null;
  name: string;
  description: string | null;
  isTemplate: boolean;
  enabled: boolean;
  steps: WorkflowStep[];
  createdAt: string;
  updatedAt: string;
}

export interface WorkflowTemplateStep {
  name: string;
  job: string;
  instruction: string | null;
  approvalPolicy: ApprovalPolicy;
  maxRetries: number;
}

export interface WorkflowTemplate {
  key: string;
  name: string;
  description: string;
  steps: WorkflowTemplateStep[];
}

export interface WorkflowRun {
  id: string;
  workflowId: string;
  workflowName: string;
  workItemId: string;
  workItemTitle: string;
  status: WorkflowRunStatus;
  currentStepOrder: number;
  currentStepId: string | null;
  createdAt: string;
  finishedAt: string | null;
}

export interface Blocker {
  id: string;
  projectId: string;
  workItemId: string;
  workItemTitle: string;
  workflowRunId: string | null;
  agentRunId: string | null;
  kind: BlockerKind;
  reason: string;
  contextSnapshot: string | null;
  status: BlockerStatus;
  resolution: BlockerResolution | null;
  resolutionNote: string | null;
  createdAt: string;
  resolvedAt: string | null;
}

export interface WorkflowRunDetail {
  run: WorkflowRun;
  agentRuns: AgentRunListItem[];
  blockers: Blocker[];
}

export interface AgentTeamMember {
  agentId: string;
  agentName: string;
  role: string | null;
}

export interface AgentTeam {
  id: string;
  projectId: string;
  name: string;
  goal: string | null;
  members: AgentTeamMember[];
}

/** CI/CD（M4） */
export interface ProjectIntegration {
  id: string;
  projectId: string;
  platform: "github" | "gitlab";
  hasToken: boolean;
  repository: string | null;
  updatedAt: string;
}

export interface CiCheck {
  name: string;
  status: string;
  conclusion: string | null;
  url: string | null;
}

export type PullRequestState = "open" | "merged" | "closed" | "unknown";
export type CiStatus = "success" | "failure" | "pending" | "none";

export interface PullRequestStatus {
  state: PullRequestState;
  ciStatus: CiStatus;
  mergeable: boolean | null;
  url: string | null;
  checks: CiCheck[];
}

export interface ReleaseResult {
  tag: string;
  url: string | null;
  message: string;
}

/** 工作项最近一次 Agent 活动（看板实时状态） */
export interface WorkItemAgentActivity {
  workItemId: string;
  agentRunId: string;
  agentName: string;
  status: AgentRunStatus;
  updatedAt: string;
}

/** M5：Skills / MCP / 审计 / 成本 */
export interface Skill {
  id: string;
  name: string;
  description: string | null;
  promptContent: string;
  tools: string | null;
  scriptPath: string | null;
  enabled: boolean;
  agentCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface McpServer {
  id: string;
  name: string;
  endpoint: string;
  transport: string;
  hasToken: boolean;
  enabled: boolean;
  agentCount: number;
  updatedAt: string;
}

export interface AuditLogEntry {
  id: string;
  projectId: string | null;
  action: string;
  targetType: string;
  targetId: string | null;
  actor: string;
  detail: string | null;
  createdAt: string;
}

export interface CostByModel {
  provider: string;
  model: string;
  promptTokens: number;
  completionTokens: number;
  cost: number;
}

export interface CostReport {
  totalPromptTokens: number;
  totalCompletionTokens: number;
  totalCost: number;
  byModel: CostByModel[];
}

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
