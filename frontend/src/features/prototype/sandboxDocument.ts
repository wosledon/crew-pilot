/**
 * 原型 HTML 沙箱文档：注入严格 CSP（禁止外部网络、表单与顶层导航），
 * iframe 侧再叠加 sandbox="allow-scripts"（无 allow-same-origin，隔离源）。
 */
const DocumentCsp =
  "default-src 'none'; style-src 'unsafe-inline'; img-src data: blob:; " +
  "script-src 'unsafe-inline'; font-src data:; connect-src 'none'; " +
  "base-uri 'none'; form-action 'none';";

export function buildSandboxDocument(html: string): string {
  const meta = `<meta http-equiv="Content-Security-Policy" content="${DocumentCsp}">`;

  if (/<head[^>]*>/i.test(html)) {
    return html.replace(/<head[^>]*>/i, (match) => `${match}${meta}`);
  }

  if (/<html[^>]*>/i.test(html)) {
    return html.replace(/<html[^>]*>/i, (match) => `${match}<head>${meta}</head>`);
  }

  return `<!doctype html><html><head>${meta}</head><body>${html}</body></html>`;
}
