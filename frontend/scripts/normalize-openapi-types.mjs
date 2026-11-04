import { readFileSync, writeFileSync } from "node:fs";

/**
 * openapi-typescript 为 .NET decimal/int 生成 `number | string`（JSON 数字可读字符串），
 * 但 API 默认序列化为 JSON number；前端类型统一收敛为 number，避免算术处被迫收窄。
 */
const target = new URL("../src/lib/api-schema.d.ts", import.meta.url);
let content = readFileSync(target, "utf8");

const before = content;
content = content
  .replaceAll("null | number | string", "null | number")
  .replaceAll("number | string", "number");

if (content === before) {
  console.error("normalize-openapi-types: 未找到需要收敛的 number | string");
  process.exitCode = 1;
} else {
  writeFileSync(target, content);
  console.log("normalize-openapi-types: 数字联合类型已收敛为 number");
}
