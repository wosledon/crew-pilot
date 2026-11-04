import type { components } from "./api-schema";

/**
 * OpenAPI 生成的契约类型（`docs/openapi.json` → `api-schema.d.ts`）。
 * 新增 API 类型优先使用这里的别名；历史手写类型在替换过程中逐步迁移。
 */
export type ApiSchemas = components["schemas"];

export type ApiWorkItemDetail = ApiSchemas["WorkItemDetailDto"];
export type ApiWorkItemListItem = ApiSchemas["WorkItemListItemDto"];
export type ApiProjectDetail = ApiSchemas["ProjectDetailDto"];
export type ApiWorkflow = ApiSchemas["WorkflowDto"];
export type ApiWorkflowRun = ApiSchemas["WorkflowRunDto"];
export type ApiBlocker = ApiSchemas["BlockerDto"];
export type ApiAgentDetail = ApiSchemas["AgentDetailDto"];
export type ApiProviderDetail = ApiSchemas["ProviderDetailDto"];
export type ApiCostReport = ApiSchemas["CostReportDto"];
export type ApiBatchWorkItemsResult = ApiSchemas["BatchWorkItemsResultDto"];
export type ApiSkill = ApiSchemas["SkillDto"];
export type ApiMcpServer = ApiSchemas["McpServerDto"];
