import type { WorkItemType } from "@/lib/types";

/**
 * 前端可见的状态流转表，与后端 WorkItemStateMachine 保持一致。
 * 用于「下一状态」下拉，避免展示非法流转。
 */
const requirementFlow: Record<string, string[]> = {
  backlog: ["ready", "rejected"],
  ready: ["inProgress", "backlog", "rejected"],
  inProgress: ["inReview", "ready"],
  inReview: ["done", "inProgress"],
  done: [],
  rejected: [],
};

const defectFlow: Record<string, string[]> = {
  new: ["confirmed", "notADefect"],
  confirmed: ["fixing", "deferred"],
  fixing: ["pendingVerification", "confirmed"],
  pendingVerification: ["closed", "fixing"],
  closed: [],
  notADefect: [],
  deferred: ["confirmed"],
};

export function nextStatuses(type: WorkItemType, status: string): string[] {
  if (type === "requirement") {
    return requirementFlow[status] ?? [];
  }
  if (type === "defect") {
    return defectFlow[status] ?? [];
  }
  return [];
}

export function statusTone(status: string): "neutral" | "primary" | "success" | "warning" | "danger" | "info" {
  switch (status) {
    case "done":
    case "closed":
      return "success";
    case "inProgress":
    case "fixing":
    case "pendingVerification":
      return "info";
    case "inReview":
    case "ready":
      return "primary";
    case "rejected":
    case "notADefect":
      return "danger";
    case "deferred":
      return "warning";
    default:
      return "neutral";
  }
}

export function priorityTone(priority: string): "neutral" | "primary" | "success" | "warning" | "danger" | "info" {
  switch (priority) {
    case "p0":
      return "danger";
    case "p1":
      return "warning";
    case "p2":
      return "info";
    default:
      return "neutral";
  }
}
