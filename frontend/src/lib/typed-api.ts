import createClient from "openapi-fetch";
import type { paths } from "./api-schema";

/**
 * OpenAPI 生成式类型客户端（openapi-fetch）。
 * 路径与请求/响应类型全部来自 `api-schema.d.ts`；动态拼接路径的旧封装仍由 `api` 提供。
 */
export const typedApi = createClient<paths>({
  credentials: "include",
});

/** 解包 openapi-fetch 结果：错误优先抛 ProblemDetails.detail，成功返回 data。 */
export async function unwrapResponse<T>(result: {
  data?: T;
  error?: unknown;
  response: Response;
}): Promise<T> {
  if (result.error !== undefined || result.data === undefined) {
    let message = result.response.statusText || `HTTP ${result.response.status}`;
    if (
      typeof result.error === "object" &&
      result.error !== null &&
      "detail" in result.error &&
      typeof result.error.detail === "string"
    ) {
      message = result.error.detail;
    }
    throw new Error(message);
  }
  return result.data;
}
