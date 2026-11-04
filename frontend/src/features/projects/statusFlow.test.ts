import { describe, expect, it } from "vitest";
import { nextStatuses, priorityTone, statusTone } from "./statusFlow";

describe("statusFlow", () => {
  it("需求状态流转表与后端状态机一致", () => {
    expect(nextStatuses("requirement", "backlog")).toEqual(["ready", "rejected"]);
    expect(nextStatuses("requirement", "inprogress")).toEqual(["inreview", "ready"]);
    expect(nextStatuses("requirement", "done")).toEqual([]);
  });

  it("缺陷状态流转包含验证与延期分支", () => {
    expect(nextStatuses("defect", "new")).toEqual(["confirmed", "notadefect"]);
    expect(nextStatuses("defect", "fixing")).toEqual(["pendingverification", "confirmed"]);
    expect(nextStatuses("defect", "deferred")).toEqual(["confirmed"]);
    expect(nextStatuses("defect", "closed")).toEqual([]);
  });

  it("任务不展示前端状态流转（仅 todo/done）", () => {
    expect(nextStatuses("task", "todo")).toEqual([]);
  });

  it("未知状态返回空数组", () => {
    expect(nextStatuses("requirement", "unknown")).toEqual([]);
  });

  it("状态与优先级映射到语义色", () => {
    expect(statusTone("done")).toBe("success");
    expect(statusTone("inprogress")).toBe("info");
    expect(statusTone("ready")).toBe("primary");
    expect(statusTone("rejected")).toBe("danger");
    expect(statusTone("deferred")).toBe("warning");
    expect(statusTone("backlog")).toBe("neutral");

    expect(priorityTone("p0")).toBe("danger");
    expect(priorityTone("p1")).toBe("warning");
    expect(priorityTone("p2")).toBe("info");
    expect(priorityTone("p3")).toBe("neutral");
  });
});
