# Crew Pilot 差距清单与实施计划

> 本文件是 `docs/PRD.md` 的逐条对照表，用于跟踪"还有哪些没实现"以及实施顺序。
> 每轮开发后更新勾选状态；状态标记：`[x]` 已完成、`[~]` 进行中/部分完成、`[ ]` 未开始。
> 最后更新：2026-11-04

## 0. 当前基线

| 项 | 状态 |
| --- | --- |
| 后端 | .NET 10 + EF Core（SQLite / PostgreSQL 双 provider）/ 142 项测试全绿 |
| 前端 | React 18 + TS + Vite；lint / format / build / 13 项 Vitest + 2 条 Playwright E2E 全绿 |
| M0 | 基本完成（项目/工作项/原型/前端骨架） |
| M1 | 主体完成，剩余列表视图/批量操作/原型增强等 |
| M2 | 完成并硬化（取消/沙箱/工具权限/LocalPath 已修） |
| M3 | 完成（工作流/审批/卡点/模板/可视化编辑器/团队与默认团队） |
| M4 | 主体完成（Token 加密、gh/glab、PR/CI 同步、发布） |
| M5 | 主体完成（Skills/MCP 配置/成本/审计），剩余 MCP 运行时与部署 |
| 工程化 | CI/Docker/compose/SignalR/PG/前端测试已落地；API 类型手写 |

## 1. 分阶段实施计划

### P0 修复"产品灵魂"硬伤（已完成 · Round 1）

- [x] 取消语义：运行中可中止模型调用；排队取消不再被 Worker 执行；取消后保留日志与工时
- [x] `Project.LocalPath` 后端 + 前端可配置（worktree/沙箱真正生效）
- [x] worktree 生命周期：失败/取消时清理；成功时保留待 PR（M4 接 PR 后自动清理）
- [x] `Agent.Tools` 工具白名单在引擎层生效
- [x] 沙箱安全：路径分隔符校验、命令白名单不经 shell、超时真正生效
- [x] 前端 ESLint 9 flat config + Prettier，`npm run lint` / `format:check` 通过
- [x] AgentRun 异常兜底：Worker 未捕获异常时落 Failed，避免永久 Running

### P1 补齐 M1 欠账（Round 2 协作/里程碑；Round 3 工作项/项目/工时）

- [x] 评论（CRUD、用户/Agent 区分、@Agent 触发 AgentRun 并回写回复）
- [x] 附件（上传/下载/删除、本地存储、20MB 限制）
- [x] 活动流 UI（项目时间线）
- [x] 里程碑（CRUD API + 甘特图标记/列表 + 迭代归属）
- [x] 项目：编辑/归档、成员管理（Agent 作为成员）、概览（Round 3；待办/卡点队列依赖 M3）
- [x] 工作项：标签/编辑/删除/指派人变更、列表视图 + 批量操作 + 看板标签筛选（Round 12）
- [x] 工作项：迭代/截止日/故事点录入 UI；Task 父子关系与状态流转
- [x] 工时：登记人归属/剩余工时扣减/按成员报表 + 迭代/日期过滤（Round 14）
- [x] 原型：URL/图片/MCP 渲染、图片上传、版本历史、需求关联 UI、CSP 加固（Round 11）
- [x] 模型供应商：默认供应商切换、模型手动增删改/启停、定时刷新（Round 8）

### P2 M3 工作流与卡点（Round 4 完成核心闭环）

- [x] `Workflow` / `WorkflowStep` 实体、迁移、CRUD API（Go 单步编排 + 模板实例化）
- [x] 审批策略：自动通过 / 仅卡点时询问 / 总是询问（每步可配，默认 BlockersOnly）
- [x] `Blocker` 卡点实体：原因、上下文快照、处理结果（继续/修改方向/取消）
- [x] 卡点队列页面：集中处理并续跑
- [x] `AgentRun` 关联工作流步骤；新增 `WaitingForHuman` 状态
- [x] `AgentTeam` / `AgentTeamMember` 实体 + API + 团队管理 UI + 项目默认团队（Round 13）
- [x] 内置三条模板流（需求交付/缺陷修复/发布）
- [x] 步骤失败自动重试；通过卡点处理实现单步重跑/改向
- [x] 前端：工作流面板 + 卡点队列页 + 可视化步骤编辑器（Round 13）

### P3 M4 CI/CD（Round 5 完成主体）

- [x] GitHub / GitLab Token 加密存储（Project 级，DPAPI）
- [x] `gh` / `glab` 进程封装：PR/MR 创建、状态查询、合并、CI 状态、Release
- [x] 工作项 ↔ PR/MR 关联与 CI 状态回写（合并后自动流转 Done/Closed）
- [x] 发布流模板（Round 4）+ 直接发布 API（tag → Release）
- [x] 前端：集成配置卡、PR/CI 状态展示与同步、发布面板
- [x] CI 状态实时推送：PR 创建/同步/合并后经 SignalR 广播工作项状态（Round 24；无 webhook，依赖同步动作）

### P4 M5 打磨（Round 6 完成主体）

- [x] Skills：实体 + CRUD + Agent 装配 + 运行时注入系统提示词
- [ ] Skills 收尾：SKILL.md 目录/脚本/资源、导入导出、脚本执行
- [x] MCP：Server 配置、认证加密、启停、连通性测试、按 Agent 授权
- [x] MCP 运行时：Streamable HTTP 客户端（initialize / tools/list / tools/call，Round 9）
- [x] MCP 运行时：stdio 传输（Round 15）、旧版 HTTP+SSE 双通道（Round 20）
- [x] MCP 收尾：工具级审计（Round 21）
- [x] 成本统计：token × 模型单价 → 项目级费用，按供应商/模型汇总
- [x] 审计日志：敏感操作留痕 + 查询 API/页面
- [x] 结构化日志（生产 JSON console，Round 14）+ 请求关联 ID
- [x] 审计日志（Round 6）
- [ ] 安全加固：密钥轮换界面、发布审批
- [ ] 部署文档 + `docker compose`

### P5 基建与质量

- [x] SignalR 实时推送（工作项状态 / AgentRun 进度 / 卡点，Round 7）
- [x] PostgreSQL 支持（Npgsql + provider 切换 + 独立迁移程序集 + compose 覆盖；Round 10）
- [x] CI（GitHub Actions：build/test/lint）+ Dockerfile + compose + 部署文档（Round 7）
- [x] OpenAPI 前端类型：spec 导出 + `api-schema.d.ts` 生成 + 核心 DTO 全部由契约派生（Round 23）；请求体表单类型仍在前端定义
- [ ] OpenAPI 生成完整客户端（替换手写 fetch 封装）
- [x] 前端测试：Vitest（13 项）+ Playwright E2E 起步（Round 16，2 条主链路）
- [ ] 测试补齐：真实 MAF 适配层、取消、沙箱、worktree、安全用例
- [ ] 文档更新：README 进度、AGENTS 现状、README.en

## 2. PRD 逐节对照

### 4.1 项目管理（P0）

- [x] 项目创建/查询/删除
- [x] 项目编辑 UI（Round 3）
- [x] 归档（Round 3：archive/unarchive）
- [x] 项目首页概览（Round 3：工作项数/工时/进行中迭代进度）
- [x] 成员管理（Round 3：Agent 作为成员、角色调整、移除保护）
- [ ] 项目可见性字段
- [ ] Git 仓库绑定（仅 `repoUrl` 字符串，无鉴权与校验）
- [x] 本地工作目录 `LocalPath` 可配置（Round 1）
- [ ] 我的待办 / 卡点队列（依赖 M3）

### 4.2 需求管理（P0）

- [x] CRUD 后端（含验收标准、优先级、故事点、迭代、截止日）
- [x] 状态流转（状态机 + 非法流转 400）
- [x] 标签（Round 3）
- [ ] 富文本描述（当前纯文本）
- [ ] 关联缺陷
- [x] 关联原型 UI（原型工坊可绑定需求/按需求过滤，Round 11）
- [x] 详情页 / 编辑 UI / 指派人变更（Round 3）
- [x] 删除 UI（后端已有，Round 3）

### 4.3 缺陷管理（P0）

- [x] 创建/查询、严重级别、复现步骤、环境、状态机
- [x] 编辑缺陷字段（Round 3）
- [ ] 测试 Agent 验证后自动流转
- [x] 关联需求 UI（Round 18：缺陷编辑绑定需求 + 详情展示）

### 4.4 原型工坊（P0）

- [x] HTML 编辑 + sandbox iframe 实时预览
- [ ] 多文件支持
- [x] 版本历史（Round 11：自动快照 + 恢复）
- [x] URL / 图片 / MCP 原型渲染（Round 11）
- [x] 图片上传 / 存储（Round 11：PNG/JPG/WEBP/GIF，10MB，只读图片接口）
- [x] 设计 MCP 客户端（配置/运行时工具，Round 9；原型内嵌渲染仍为引用）
- [x] 原型 ↔ 需求关联 UI（Round 11）
- [x] 严格 CSP 与沙箱加固（Round 11：CSP meta 注入 + sandbox iframe）

### 4.5 工时管理（P0）

- [x] 预估/已耗/剩余字段；手动登记；AgentRun 1:1 折算
- [x] 燃尽图、甘特图、工时报表（基础版）
- [x] 登记人归属（Round 3：Owner / Agent 成员）
- [x] 剩余工时自动扣减（Round 3）
- [x] token 成本视图（Round 6，支持迭代/日期过滤，Round 14）
- [x] 人力成本视图（工时 × 成员时薪，Round 19）
- [x] 报表按迭代/日期过滤（Round 14）
- [x] 估算偏差分析（按类型预估/已耗/偏差%/超支项，Round 19）
- [ ] 甘特图按成员泳道汇总

### 4.6 任务与迭代看板（P0/P1）

- [x] 迭代 CRUD/开始/完成/删除、燃尽图
- [x] 看板拖拽流转（前端状态表 + 后端状态机）
- [x] 迭代编辑 UI（Round 25：编辑弹窗复用创建表单 + 后端 PUT）
- [x] 工作项排入迭代的 UI（Round 3：编辑弹窗）
- [x] Task 拆解（父子关系）/ 独立流转（Round 3）
- [x] 看板筛选（指派人/优先级/标签）、WIP 提示（Round 25：进行中列 5 项上限警示）
- [x] 列表视图 + 批量操作（Round 12）
- [ ] 500+ 卡片性能验证

### 4.7 甘特图（P1）

- [x] 迭代时间线 + 工作项截止日 + 逾期标红 + 工时报表
- [x] 里程碑渲染（时间线标记 + 列表 + 迭代归属）
- [ ] 依赖关系（FS/SS）
- [ ] 关键路径（P2）
- [ ] 工时泳道
- [ ] PM Agent 自动更新进度/周报

### 4.8 评论、附件与活动流（P0/P1）

- [x] 评论（CRUD、@Agent 触发、Agent 回复写回评论线程）
- [x] 附件上传/下载/删除（本地存储，文件大小限制）
- [x] 活动流记录（后端写入）
- [x] 活动流 UI（项目时间线）
- [ ] 活动流查询过滤（按工作项/事件类型）

### 4.9 权限与审计（P0/P1）

- [x] 单用户 Owner 密码认证（可选启用，Cookie 登录；Round 17）
- [x] 敏感操作审计日志（Round 6：删除/集成/发布等留痕 + 查询页）
- [x] 机密加密存储且不出现在响应（DPAPI）
- [ ] 密钥查看/轮换界面

### 5.1 模型供应商（P0）

- [x] 自定义 OpenAI 兼容端点 + DPAPI 加密 + 连通性测试
- [x] models.dev 目录同步（手动 + 定时刷新，失败保留缓存；Round 8）
- [x] 默认供应商切换（API + UI，Round 8）
- [x] 模型手动增删改/启停（API + UI，Round 8）
- [x] token 用量与费用汇总（Round 6：项目成本报表）
- [x] 成本按迭代/日期维度过滤（Round 14）

### 5.2 Agent 定义（P0）

- [x] CRUD、岗位、模型、系统提示词、工具/命令白名单字段
- [x] 工具权限真正生效（Round 1）
- [x] 提示词模板变量替换（{{title}}/{{requirement}}/{{acceptanceCriteria}}/{{type}}/{{status}}/{{priority}}，Round 9）
- [ ] 沙箱策略字段
- [x] 试运行可选需求并注入上下文（Round 16）

### 5.3 Agent 团队（P1）

- [x] 实体 + 基础 API（Round 4）
- [x] 团队管理 UI + 项目默认团队（工作流按岗位从默认团队解析 Agent，Round 13）
- [ ] 工作项类型级默认团队（当前仅项目级）

### 5.4 工作流（P0 单步 → P1 编排）

- [x] 工作流定义/步骤/审批策略（Round 4）
- [x] 卡点机制（Round 4）
- [x] 三条内置模板（Round 4）
- [x] 实例化运行、失败重试、卡点续跑（Round 4；「全程自动驾驶」即默认策略）
- [x] 单步 AgentRun（手动指派/认领）
- [x] 可视化步骤编排编辑器（Round 13：步骤增删/排序/Agent/岗位/审批/重试/契约）

### 5.5 Skills（P1）

- [x] 实体与 CRUD（名称/描述/提示词/工具白名单/脚本目录）
- [x] Agent 装配（AgentSkill）与运行时注入系统提示词
- [x] 前端管理页与 Agent 能力装配 UI
- [ ] SKILL.md 目录结构、脚本/资源、导入导出

### 5.6 运行引擎（P0）

- [x] MAF 收敛在 `AgentEngine` 抽象后
- [x] 内置工具：文件读写/列目录/白名单命令
- [x] 项目目录隔离 + git worktree（Round 1 打通 LocalPath）
- [~] worktree 清理/合并/PR：失败/取消已清理，成功保留待 M4 接 PR
- [~] 运行详情：日志/工具调用时间线、实时刷新（Round 22）；流式 token 输出待做
- [ ] 多 Agent 协作
- [x] 沙箱加固（路径逃逸/命令注入/超时，Round 1）

### 5.7 MCP（P1）

- [x] Server 配置、认证加密、启停、连通性测试
- [x] 按 Agent 授权（AgentMcpServer）
- [x] 运行时 MCP 客户端（Streamable HTTP：initialize/tools/list/tools/call，工具注入 AgentEngine；Round 9）
- [x] stdio（Round 15）与旧版 HTTP+SSE 双通道（Round 20）；Streamable HTTP 为主路径
- [ ] 设计 MCP 链路（原型 ↔ MCP）

### 5.8 单用户协同（P0）

- [x] 卡点队列（Round 4）
- [~] 审批策略默认 + 每步覆盖（每步已支持，全局默认配置 UI 待补）
- [x] 取消/接管入口（Round 1）
- [x] @Agent 触发（Round 2：评论提及 → AgentRun → 回复写回）
- [~] 项目概览（Round 3）与工作流运行面板；"看而不等"实时仪表盘待 M3 收尾

### 5.9 Agent 认领闭环（P0）

- [x] 指派 → 入队 → 执行 → 日志/产物/token → 工时 → PR 回填
- [x] 看板卡片显示 Agent 头像/实时状态（Round 18：最近 Run 徽标 + 5s 刷新/SignalR）
- [x] 工作流完成时自动流转工作项状态（InProgress→InReview / Fixing→PendingVerification）
- [ ] Agent 作为项目成员/指派人
- [x] PR 创建与合并（Round 24：创建/同步已有，新增 `POST .../pull-request/merge` + 前端确认合并）
- [x] 失败重试（Round 25：失败/取消 Run 一键重试，工作流 Run 走卡点重试）

### 6. CI/CD（P1 · Round 5）

- [x] GitHub `gh` 封装（PR 创建/状态/合并/Release）
- [x] GitLab `glab` 封装（MR 创建/状态/合并/Release）
- [x] 发布流（模板 + 直接 Release API）
- [x] CI 状态回写（手动同步，合并自动流转）+ PR 合并 API/UI（Round 24）
- [x] CI 状态实时推送（SignalR 阶段）

### 8. 架构与安全

- [x] 分层（Api → Agents/Infrastructure → Core）
- [x] ProblemDetails 错误处理
- [ ] SignalR
- [ ] PostgreSQL
- [x] 审计日志（Round 6）
- [ ] 多用户 / RBAC（单用户产品不在范围）
- [x] CSP（原型，Round 11）

## 3. 已知缺陷 / 安全债

| # | 问题 | 位置 | 状态 |
| --- | --- | --- | --- |
| 1 | 排队中 Run 取消后仍会被 Worker 执行 | `AgentRunExecutor.ExecuteAsync` | ✅ Round 1 |
| 2 | 取消不中止正在进行的模型调用 | `AgentRunCancellationRegistry` | ✅ Round 1 |
| 3 | Agent 工具路径 `StartsWith` 可逃逸到同前缀兄弟目录 | `AgentTools.ResolvePath` | ✅ Round 1 |
| 4 | 命令白名单只校验首 token，`cmd /c` 可被 `&&` 绕过 | `AgentTools` | ✅ Round 1（不再经 shell） |
| 5 | 超时无效（`ReadToEnd` 先于 `WaitForExit`） | `AgentTools` / `ProcessRunner` | ✅ Round 1 |
| 6 | `Agent.Tools` 字段被引擎忽略 | `MafAgentEngine` | ✅ Round 1 |
| 7 | `Project.LocalPath` 无 API/UI，worktree 永不生效 | `ProjectsController` | ✅ Round 1 |
| 8 | `npm run lint` 失败（无 eslint.config.js） | `frontend/` | ✅ Round 1（含 Prettier） |
| 9 | Worker 未捕获异常导致 Run 永久 Running | `AgentRunWorker` | ✅ Round 1 |
| 10 | 工时登记人缺失 | `WorkItemsController.AddWorkLog` | ✅ Round 3 |
| 11 | `UpdateWorkItemRequest` 无法改指派人/缺陷字段 | `Contracts/WorkItems.cs` | ✅ Round 3 |
| 12 | Task 状态流转 400、父子关系无法设置 | `WorkItemsController` | ✅ Round 3 |
| 13 | 前端手写 API 类型，未用 OpenAPI 生成 | `frontend/src/lib/types.ts` | ✅ Round 23：响应 DTO 由契约派生 |
| 14 | 无 PostgreSQL provider，连接串切换无效 | `Program.cs` | P5 |
| 15 | 无认证 / 审计日志缺失 | 全局 | 认证 ✅ Round 17；审计 ✅ Round 6 |

## 4. 每轮工作日志

### Round 25（2026-11-04）

- AgentRun 重试（PRD 5.9）：`POST /api/agent-runs/{id}/retry` —— 仅失败/已取消可重试（工作流 Run 走卡点重试），复制提示词创建新 Run 并入队，写活动流；前端执行记录新增「重试」按钮
- 迭代编辑 UI（PRD 4.6）：新建/编辑共用弹窗，编辑回填名称/目标/日期/里程碑；后端 PUT 生效
- 看板筛选与 WIP（PRD 4.6）：指派人/优先级/标签三筛选；进行中列显示 WIP 5 项上限，超限红色警示
- 测试：140 → 142（重试成功链路、非失败状态 400），修正测试间 Fake 引擎失败计数竞态；前端 lint / format / test:run / build / E2E 全绿

### Round 24（2026-11-04）

- PR 合并（PRD 5.9 / M4）：新增 `POST /api/workitems/{id}/pull-request/merge` —— 调 gh/glab 合并、回拉状态/CI、合并后自动推进工作项（需求→Done / 缺陷→Closed）、写活动流
- CI 实时推送：PR 创建/同步/合并成功后经 `IRealtimeNotifier.WorkItemChangedAsync` 广播（前端 SignalR 已按事件刷新）
- 前端：PR 区域新增「合并 PR」按钮（ConfirmDialog 二次确认、状态徽标刷新）
- 测试：139 → 140（真实合并调用 + 状态回写 + 自动流转），`dotnet test` 全绿；前端 lint / format / test:run / build / E2E 全绿

### Round 23（2026-11-04）

- OpenAPI 契约迁移（P5 收尾）：重新导出 `docs/openapi.json`（含认证/人力成本/关联需求/Agent 活动等新 DTO）
- `api-schema.d.ts` 数字类型收敛脚本（`number | string` → `number`，与 .NET 默认 JSON 序列化一致），`npm run api:types` 一条命令完成生成 + 归一
- `frontend/src/lib/types.ts` 全量改为从生成契约派生：Project/WorkItem/Iteration/Prototype/Comment/Report/Provider/Agent/Run/Workflow/Blocker/Team/CI/技能/MCP/审计/成本；仅保留领域联合类型（状态枚举）与前端错误类
- 测试：后端 139 项不变；前端 lint / format / test:run（13）/ build 全绿；生成结果可复现（CI 校验依旧）
- 验证：`npm run api:types` 无 diff、`npm run lint` / `format:check` / `test:run` / `build` 全绿

### Round 22（2026-11-04）

- 运行监控（PRD 5.9）：新增 `AgentRunDetailDialog` —— 状态/触发/耗时/token、提示词、输出、错误、工作目录，执行日志按序号 + kind + 时间线展示；运行中每 2s 自动刷新并显示「实时刷新中」
- 执行记录列表：显示创建时间、新增「查看详情」入口（工作项详情/项目页均复用 `AgentRunPanel`）
- 测试：后端 139 项不变；前端 lint / format / test:run（13）/ build / E2E（2）全绿
- 验证：`npm run lint` / `format:check` / `test:run` / `test:e2e` / `build` 全绿

### Round 21（2026-11-04）

- MCP 工具级审计（PRD 5.7）：新增 `McpRouteInvoker` 包装每次 `tools/call`，记录服务器/工具/耗时/成功失败/错误（不含参数原文）；`McpToolSession` 暴露运行内调用审计队列
- `AgentRunExecutor` 在运行结束（含失败/取消）统一落库 `AuditLog`：action `mcp.tool.called`、projectId、targetId `server:tool`、detail JSON（runId/workItemId/agent/elapsedMs）
- 测试：133 → 139（成功/失败审计、未知工具、工具名清洗），`dotnet test` 全绿；前端 lint / format / test / build 仍绿
- 验证：`dotnet test`、`npm run lint` / `format:check` / `test:run` / `build` 全绿

### Round 20（2026-11-04）

- MCP 旧版 HTTP+SSE 双通道（PRD 5.7）：新增 `SseMcpClient`（GET 长连接、endpoint 事件、消息 POST、响应经事件流按 id 匹配、心跳忽略、超时/释放）；`McpToolSession` 按 transport 选择 HTTP/旧版 SSE/stdio
- MCP 连通性测试页对 SSE 走真实握手并报告工具数量；旧版 SSE 单测（假服务端事件流 + Bearer + sessionId 校验）
- 测试：132 → 133，`dotnet test` 全绿；前端 lint / test / build 仍绿
- 验证：`dotnet test`、`npm run lint` / `test:run` / `build` 全绿

### Round 19（2026-11-04）

- 人力成本（PRD 4.5/4.8）：`ProjectMember.HourlyRate` + 迁移（SQLite/PG）；成员卡可直接编辑时薪；`GET /api/projects/{id}/human-cost-report`（工时 × 时薪，按成员汇总）；甘特页新增人力成本卡
- 估算偏差（PRD 4.8）：`GET /api/projects/{id}/estimate-deviation`（按类型：预估/已耗/偏差小时/偏差%/超支项数）；甘特页新增估算偏差卡（超支高风险标记）
- 测试：131 → 132（人力成本 + 偏差计算），`dotnet test` 全绿；前端 13 项 + E2E 2 条仍通过
- 验证：`dotnet test`、`npm run lint` / `format:check` / `test:run` / `test:e2e` / `build` 全绿

### Round 18（2026-11-04）

- 缺陷↔需求关联（PRD 4.3）：创建/编辑缺陷可绑定同项目需求（`RelatedRequirementId` 校验）、详情返回需求标题；前端编辑弹窗下拉 + 详情展示
- 看板 Agent 实时状态（PRD 5.9）：`GET /api/projects/{id}/agent-activity` 返回每个工作项最近一次 Run（进行中优先）；看板卡片显示 🤖 Agent + 状态徽标，5s 轮询（SignalR 事件同时刷新）
- 测试：128 → 131（缺陷关联/跨项目校验、Agent 活动），`dotnet test` 全绿；前端 13 项 + E2E 2 条仍通过
- 验证：`dotnet test`、`npm run lint` / `format:check` / `test:run` / `test:e2e` / `build` 全绿

### Round 17（2026-11-04）

- 单用户认证（PRD 4.9 收尾）：`Auth:Password` 启用后 Cookie 登录（HttpOnly / SameSite=Strict / 7 天滑动过期）；`AuthGateMiddleware` 保护除 login/status/health/openapi 外的全部 API；常量时间密码比较；登录/退出/状态 API
- 前端：认证门禁 + 登录页（配置启用时才出现）；401 自动回到登录页；侧边栏退出按钮
- 测试：123 → 128（认证关闭默认放行、启用后 401/登录 Cookie/退出、密码比较 3 项等），`dotnet test` 全绿；E2E 2 条仍通过（默认认证关闭）
- 默认保持关闭：未配置密码时行为与之前完全一致（自托管开箱即用）
- 验证：`dotnet test`、`npm run lint` / `format:check` / `test:run` / `test:e2e` / `build` 全绿

### Round 16（2026-11-04）

- 试运行上下文（PRD 5.2）：`POST /api/agents/{id}/runs` 支持 `workItemId`，注入标题/类型/状态/描述/验收标准；前端试运行弹窗增加项目/工作项选择；集成测试断言提示词内容
- Playwright E2E 起步（P5）：`frontend/e2e/smoke.spec.ts` 覆盖「创建项目 → 工作项 → 看板查看」「Agent 页加载」；配置自动启动 API（http profile）与 Vite dev server；CI 新增独立 e2e job（安装 Chromium、失败上传 trace）
- 工程：`.gitignore` 忽略 Playwright 产物；format 检查扩展到 e2e 与配置文件
- 测试：后端 122 → 123（试运行上下文），E2E 2/2 通过；前端 13 项仍绿
- 验证：`dotnet test`、`npm run lint` / `format:check` / `test:run` / `test:e2e` / `build` 全绿

### Round 15（2026-11-04）

- MCP stdio 传输（PRD 5.7）：新增 `IMcpSession` 抽象 + `StdioMcpClient`（子进程 + 换行 JSON-RPC，stderr 排空、超时、进程树清理）；`McpToolSession` 按配置 transport 选择 HTTP/stdio
- 单测：stdio 行协议（initialize/tools/list/tools/call，Pipe 假服务端）+ 命令行拆分
- OpenAPI（P5）：导出 `docs/openapi.json`；引入 openapi-typescript，生成 `src/lib/api-schema.d.ts` + `src/lib/contracts.ts` 契约别名；`npm run api:types`；CI 增加「生成结果无 diff」校验
- 测试：120 → 122（stdio 协议/分词），`dotnet test` 全绿；前端 13 项仍绿
- 验证：`dotnet test`、前端 `lint` / `format:check` / `test:run` / `build` 全绿

### Round 14（2026-11-04）

- 报表过滤：工时报表与成本报表支持 `iterationId` / `from` / `to`；前端甘特页新增「报表过滤」控件（迭代 + 日期范围 + 清除），工时与成本卡联动
- 结构化日志：非开发环境改为 JSON console 日志（UTC 时间戳、Scope）；新增 `X-Correlation-Id` 响应头
- 集成测试：工时按迭代+日期过滤；健康检查关联 ID；`dotnet test` 全绿（120 项）
- 冒烟验证：Production 模式启动 API，日志行为 JSON、`/health` 正常
- 验证：前端 `lint` / `format:check` / `test:run` / `build` 全绿

### Round 13（2026-11-04）

- M3 收尾：
  - 工作流可视化编辑器：前端步骤增删/上下移、指定 Agent 或岗位、审批策略、重试次数、输入/输出契约；新建与编辑（后端 CRUD 已有）
  - Agent 团队：项目页团队管理卡（建队/成员装配/删除）；`Project.DefaultTeamId` + `PUT /api/projects/{id}/default-team`（同项目校验）
  - 默认团队生效：工作流步骤未显式指定 Agent 时，优先按岗位从项目默认团队选择，再回退全局 Agent
- 测试：117 → 119（默认团队解析 Agent 跑通单步工作流、跨项目团队校验），`dotnet test` 全绿；前端 13 项仍绿
- 验证：`dotnet test`、`npm run lint` / `format:check` / `test:run` / `build` 全绿

### Round 12（2026-11-04）

- 工作项列表视图与批量操作（P1）：
  - `POST /api/workitems/batch`：status / assign / delete，逐项返回结果（失败不中断），复用状态机；批量删除写审计
  - 前端项目页新增「卡片 / 列表」切换；列表表格（类型/状态/优先级/指派人/工时）+ 多选；批量状态、批量指派、批量删除（确认框）与全选
- 看板标签筛选：标签下拉（来自当前类型全部工作项）+ 卡片显示标签；后端 `tag` 过滤已有能力
- 测试：113 → 117（批量状态/指派/删除/非法载荷），`dotnet test` 全绿；前端 13 项仍绿
- 验证：`dotnet test`、`npm run lint` / `format:check` / `test:run` / `build` 全绿

### Round 11（2026-11-04）

- 原型工坊增强（PRD 4.4）：
  - 版本历史：`PrototypeVersion` 实体 + 迁移（SQLite/PostgreSQL 各一）；创建/保存自动快照；列表与恢复 API（恢复生成新版本，不丢历史）
  - 图片原型：`POST /api/projects/{id}/prototype-images` 上传（PNG/JPG/WEBP/GIF，10MB）；`GET /api/prototype-images/{**path}` 只读访问；图片原型只接受存储路径
  - URL 原型：http(s) 绝对地址校验，前端 sandbox iframe 预览 + 外链
  - MCP 原型：引用地址/标识展示（运行时 MCP 工具已在 Round 9）
  - 需求关联：创建/更新可绑定需求（同项目校验），列表按需求过滤；前端需求下拉
  - CSP 加固：HTML 预览注入严格 CSP（`default-src 'none'`、禁止外网/表单/顶层跳转）+ sandbox iframe
  - 前端：类型化预览（HTML/URL/图片/MCP）、图片上传、版本历史面板与恢复
- 测试：109 → 113（版本快照/恢复、URL 校验、图片上传与字节读取、需求关联过滤），`dotnet test` 全绿；前端 10 → 13（CSP 注入 3 项）
- 验证：`dotnet test`、`npm run lint` / `format:check` / `test:run` / `build` 全绿

### Round 10（2026-11-04）

- PostgreSQL（P5）：
  - `Database:Provider` 切换 Sqlite / PostgreSql；独立迁移程序集 `CrewPilot.Infrastructure.PostgreSql`（设计时工厂 + InitialCreate 迁移），SQLite 迁移保持不变
  - `docker-compose.postgres.yml` 覆盖：postgres:17 + healthcheck + 自动迁移；`docs/DEPLOY.md` 增加 PG 环境变量/迁移/备份说明
  - 冒烟验证：`Database__Provider=PostgreSql` + `AutoMigrate=false` 启动 API，`/health` 返回 `{"status":"ok","database":"PostgreSql"}`
- 跨平台机密保护：非 Windows 使用 `AesGcmSecretProtector`（密钥文件 `App_Data/secrets.key`，0600 权限），Docker/Linux 可正常加密 API Key / Token；Windows 继续 DPAPI
- 运维：新增 `GET /health` 存活探针（返回 database provider）
- 测试：105 → 109（AES-GCM 往返/密钥复用/篡改、health），`dotnet test` 全绿；前端 10 项仍绿
- 说明：本机无 PostgreSQL/Docker，未做真实 PG 连接与镜像构建

### Round 9（2026-11-04）

- MCP 运行时（PRD 5.7）：
  - `Infrastructure.Mcp.McpClient`：Streamable HTTP JSON-RPC（initialize / notifications/initialized / tools/list / tools/call），支持 JSON 与 SSE 响应、Bearer 认证、Mcp-Session-Id
  - `McpToolSession`：按 Agent 绑定的 Server 建连、工具名清洗去重（`mcp_{server}_{tool}`）、失败降级；运行结束释放连接
  - AgentEngine：MCP 工具包装为 AIFunction 注入 MAF；`AgentRunRequest` 增加 McpTools/McpInvoker
- 提示词模板（PRD 5.2）：`PromptTemplate` 支持 `{{title}}/{{requirement}}/{{description}}/{{acceptanceCriteria}}/{{type}}/{{status}}/{{priority}}/{{workItemId}}`，系统提示词与 Skills 内容均渲染
- 测试：98 → 105（MCP 客户端协议/参数校验/认证头、模板渲染、模板变量端到端），`dotnet test` 全绿；前端 10 项仍绿
- 验证：`dotnet test`、前端 `lint` / `format:check` / `test:run` / `build` 全绿

### Round 8（2026-11-04）

- 供应商收尾（PRD 5.1）：
  - `PUT /api/providers/{id}/default`：全局唯一默认供应商（停用不可设默认）+ 集成测试
  - 前端：设为默认按钮、模型目录手动增删改/启停弹窗（价格/上下文/能力标签）
  - `ProviderCatalogRefreshService`：models.dev 每 24h 定时刷新（可配置/可关闭，失败保留缓存，测试禁用以避免网络）
- 前端测试基建：Vitest + Testing Library + jsdom + jest-dom；新增 10 项测试（状态流转/格式化/Badge）；CI 增加 `test:run`
- 测试：后端 97 → 98，前端 0 → 10，全部绿
- 验证：`dotnet test`、`npm run test:run`、`lint`、`format:check`、`build` 全绿

### Round 7（2026-11-04）

- P5 基建：
  - SignalR：`/hubs/realtime` Hub + `IRealtimeNotifier` 领域事件广播；工作项状态、AgentRun 状态、卡点创建/解决实时推送
  - 前端：`@microsoft/signalr` 自动重连客户端，收到事件按查询根键刷新 TanStack Query 缓存；Vite `/hubs` WebSocket 代理
  - CI：GitHub Actions（backend restore/build/test + frontend ci/lint/format/build）
  - 部署：Api Dockerfile、前端 Dockerfile + nginx 反代（/api、/hubs、SPA fallback）、docker-compose、`.dockerignore`、`docs/DEPLOY.md`
  - 启动配置：`Database:AutoMigrate` 控制迁移（开发默认开、容器置 true）
- 测试：96 → 97（SignalR negotiate），`dotnet test` 全绿
- 验证：`npm run lint` 0 error、`format:check` 通过、`npm run build` 通过
- 说明：本机无 Docker，镜像未实际构建；CI 配置按标准镜像编写

### Round 6（2026-11-04）

- M5 打磨：
  - Skills：`Skill` + `AgentSkill` 实体/迁移、CRUD API、Agent 装配、运行时把启用技能注入系统提示词；前端 Skills 管理页 + Agent 能力装配弹窗
  - MCP：`McpServer` + `AgentMcpServer` 实体/迁移、认证 DPAPI 加密、CRUD/测试 API、按 Agent 授权；前端 MCP 管理页
  - 成本：AgentRun token × 模型单价的项目成本报表，按供应商/模型汇总；前端成本卡（甘特页）
  - 审计：`AuditLog` 实体/迁移、删除/集成/发布等敏感操作留痕、查询 API + 审计页
  - 供应商：模型目录手动增删改/启停 API + 集成测试
- 测试：90 → 96（Skills CRUD/注入、MCP 脱敏/测试/授权校验、成本报表、审计、模型 CRUD），`dotnet test` 全绿
- 验证：`npm run lint` 0 error、`format:check` 通过、`npm run build` 通过；i18n key 中英一致

### Round 5（2026-11-04）

- M4 CI/CD：
  - `ProjectIntegration` 实体（DPAPI 加密 Token）+ 迁移；WorkItem 增加 `PrState` / `CiStatus`
  - `IGitCliService` / `GitCliService`：gh/glab PR/MR 创建、状态查询（含 CI checks）、合并、Release；Token 仅通过环境变量注入，不落日志
  - API：项目集成配置/测试/删除、发布；工作项 PR 创建/查询/同步（合并自动流转 Done/Closed）
  - 前端：CI/CD 集成配置卡、工作项 PR/CI 状态与同步、项目发布面板；中英文案齐备
- 工程：`ProcessRunner` 迁至 Infrastructure 并支持环境变量注入
- 测试：86 → 90（集成配置脱敏/测试、PR 创建/同步/合并流转、发布、未配置场景），`dotnet test` 全绿
- 验证：`npm run lint` 0 error、`format:check` 通过、`npm run build` 通过

### Round 4（2026-11-04）

- M3 核心：
  - 新增 `Workflow` / `WorkflowStep`（审批策略、岗位解析、重试次数、输入输出契约）、`WorkflowRun`、`Blocker`、`AgentTeam` 实体与迁移
  - `WorkflowRunner`：AgentRun 串联、`Always` 审批卡点、`[[BLOCKER]]` 标记识别、失败自动重试后落失败卡点、卡点处理续跑/改向/取消、完成自动流转工作项
  - API：工作流 CRUD、模板实例化、启动运行、运行列表/详情/取消、卡点队列/处理、AgentTeam 基础 CRUD
  - 三条内置模板：需求交付、缺陷修复、发布
  - 前端：项目工作流面板（模板创建/启动/运行记录/运行详情）、卡点队列页与子导航
- 测试基础设施：修复 WebApplicationFactory 共享连接导致的并发 SQLite 错误（每 DbContext 独立连接 + 共享内存库）
- 测试：80 → 86（6 个工作流场景：自动链、审批卡点、Agent 卡点改向、失败重试、模板实例化、非法启动），`dotnet test` 全绿
- 验证：`npm run lint` 0 error、`format:check` 通过、`npm run build` 通过

### Round 3（2026-11-04）

- 工作项：
  - `Tags` 字段 + 迁移，列表按标签过滤；前端卡片展示标签、新建/编辑可填
  - `PUT /api/workitems/{id}` 支持指派人、缺陷字段、分支/PR、Task 父项；前端编辑弹窗
  - Task 支持父子关系与 todo/done 状态流转（走 Core 状态机）
  - 详情弹窗提供编辑/删除入口
- 项目：
  - 项目编辑弹窗；archive/unarchive API 与前端按钮
  - 成员 API（Agent 作为成员、角色调整、Owner 保护、删除前评论保护）+ 前端成员卡片
  - 项目概览（工作项数 / 工时 / 进行中迭代进度）
- 工时：
  - 手登工时归属 Owner、AgentRun 折算归属 Agent 成员
  - 剩余工时随登记自动扣减；报表新增按登记成员分组
- 测试：72 → 80（工作项 5 / 项目 3 / 报表断言），`dotnet test` 全绿
- 验证：`npm run lint` 0 error、`format:check` 通过、`npm run build` 通过

### Round 2（2026-11-04）

- 协作：
  - 评论 API（列表/创建/删除）+ 前端工作项详情弹窗（评论线程）
  - @Agent：评论提及 → 创建 `mention` AgentRun → 执行完成后以 Agent 身份回写评论
  - 附件 API（multipart 上传/下载/删除，本地存储 20MB 上限）+ 前端上传/下载/删除
  - 活动流前端时间线（项目详情页）
- 里程碑：
  - `Milestone` CRUD API，`Iteration.MilestoneId` + EF 迁移（SetNull）
  - 迭代创建可关联里程碑；甘特图里程碑标记/列表/删除
- 测试：61 → 72（评论 4 / 附件 3 / 里程碑 4 / 其他），`dotnet test` 全绿
- 验证：`npm run lint` 0 error、`format:check` 通过、`npm run build` 通过

### Round 1（2026-11-04）

- 建立本文件
- P0 全部完成：
  - `AgentRunCancellationRegistry`：运行中取消可中止引擎调用；排队取消由状态守卫跳过
  - 取消/失败路径补工时折算与活动流；成功路径保留 worktree 待 PR
  - `AgentTools`：工具白名单生效、路径沙箱修复、命令不经 shell、超时/取消真正生效
  - `MafAgentEngine`：传入工具列表与取消令牌，OCE 向上传递
  - `AgentRunWorker`：未捕获异常兜底落 Failed
  - `Project.LocalPath`：DTO/创建/更新/校验 + 前端新建项目字段
  - 前端：ESLint 9 flat config、Prettier 配置与全量格式化
  - 测试：47 → 61（新增工具/取消/本地目录用例），`dotnet test` 全绿
  - 验证：`npm run lint` 0 error、`npm run format:check` 通过、`npm run build` 通过
