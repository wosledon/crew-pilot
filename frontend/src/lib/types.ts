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
