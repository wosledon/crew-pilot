import { describe, expect, it } from "vitest";
import { formatDateTime, formatHours } from "./format";

describe("format", () => {
  it("formatHours 空值显示占位符", () => {
    expect(formatHours(null)).toBe("—");
  });

  it("formatHours 按小时输出", () => {
    expect(formatHours(0)).toBe("0h");
    expect(formatHours(2.5)).toBe("2.5h");
  });

  it("formatDateTime 非法日期原样返回", () => {
    expect(formatDateTime("not-a-date")).toBe("not-a-date");
  });

  it("formatDateTime 合法日期走 Intl 本地化", () => {
    const formatted = formatDateTime("2026-11-04T12:00:00Z");
    expect(formatted).not.toBe("");
    expect(formatted).not.toBe("2026-11-04T12:00:00Z");
  });
});
