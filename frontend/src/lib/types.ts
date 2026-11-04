/** 与后端 DTO 对齐的前端类型（后续切换为 OpenAPI 生成客户端） */

export type ProjectStatus = "active" | "archived";

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
  status: ProjectStatus;
  workItemCount: number;
  createdAt: string;
  updatedAt: string;
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
}

export type AgentRunStatus = "queued" | "running" | "succeeded" | "failed" | "canceled";

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
