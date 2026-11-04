import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { Badge } from "./Badge";

describe("Badge", () => {
  it("渲染文案与语气样式", () => {
    render(<Badge tone="success">已完成</Badge>);
    const badge = screen.getByText("已完成");
    expect(badge).toBeInTheDocument();
    expect(badge.className).toContain("text-success");
  });
});
