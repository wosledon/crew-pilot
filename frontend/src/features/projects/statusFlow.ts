import type { WorkItemType } from "@/lib/types";

/**
 * 前端可见的状态流转表，与后端 WorkItemStateMachine 保持一致。
 * 用于「下一状态」下拉，避免展示非法流转。
 * 状态字符串统一小写（与 API 响应及 i18n key 一致）。
 */
const requirementFlow: Record<string, string[]> = {
  backlog: ["ready", "rejected"],
  ready: ["inprogress", "backlog", "rejected"],
  inprogress: ["inreview", "ready"],
  inreview: ["done", "inprogress"],
  done: [],
  rejected: [],
};

const defectFlow: Record<string, string[]> = {
  new: ["confirmed", "notadefect"],
  confirmed: ["fixing", "deferred"],
  fixing: ["pendingverification", "confirmed"],
  pendingverification: ["closed", "fixing"],
  closed: [],
  notadefect: [],
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

export function statusTone(
  status: string,
): "neutral" | "primary" | "success" | "warning" | "danger" | "info" {
  switch (status) {
    case "done":
    case "closed":
      return "success";
    case "inprogress":
    case "fixing":
    case "pendingverification":
      return "info";
    case "inreview":
    case "ready":
      return "primary";
    case "rejected":
    case "notadefect":
      return "danger";
    case "deferred":
      return "warning";
    default:
      return "neutral";
  }
}

export function priorityTone(
  priority: string,
): "neutral" | "primary" | "success" | "warning" | "danger" | "info" {
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
