import { describe, expect, it } from "vitest";
import { buildSandboxDocument } from "./sandboxDocument";

describe("buildSandboxDocument", () => {
  it("在已有 <head> 中注入 CSP", () => {
    const html = "<html><head><title>x</title></head><body><h1>hi</h1></body></html>";
    const document = buildSandboxDocument(html);
    expect(document).toContain('http-equiv="Content-Security-Policy"');
    expect(document).toContain("default-src 'none'");
    expect(document.indexOf("Content-Security-Policy")).toBeLessThan(document.indexOf("<title>"));
  });

  it("没有 head 但有 html 时补 head", () => {
    const document = buildSandboxDocument("<html><body><p>x</p></body></html>");
    expect(document).toContain("<head><meta http-equiv");
  });

  it("片段自动包装为完整文档", () => {
    const document = buildSandboxDocument("<p>片段</p>");
    expect(document.startsWith("<!doctype html>")).toBe(true);
    expect(document).toContain("<p>片段</p>");
    expect(document).toContain("connect-src 'none'");
  });
});
