# Crew Pilot 差距清单与实施计划

> 本文件是 `docs/PRD.md` 的逐条对照表，用于跟踪"还有哪些没实现"以及实施顺序。
> 每轮开发后更新勾选状态；状态标记：`[x]` 已完成、`[~]` 进行中/部分完成、`[ ]` 未开始。
> 最后更新：2026-11-04

## 0. 当前基线

| 项 | 状态 |
| --- | --- |
| 后端 | .NET 10 + EF Core（仅 SQLite）/ 98 项测试全绿 |
| 前端 | React 18 + TS + Vite；lint / format / build / 10 项 Vitest 全绿 |
| M0 | 基本完成（项目/工作项/原型/前端骨架） |
| M1 | 主体完成，剩余列表视图/批量操作/原型增强等 |
| M2 | 完成并硬化（取消/沙箱/工具权限/LocalPath 已修） |
| M3 | 核心闭环完成（工作流/审批/卡点队列/模板/团队实体） |
| M4 | 主体完成（Token 加密、gh/glab、PR/CI 同步、发布） |
| M5 | 主体完成（Skills/MCP 配置/成本/审计），剩余 MCP 运行时与部署 |
| 工程化 | CI/Docker/compose/SignalR/前端测试已落地；无 PostgreSQL、API 类型手写 |

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
- [~] 工作项：标签/编辑/删除/指派人变更已完成；列表视图 + 批量操作 + 看板筛选待补
- [x] 工作项：迭代/截止日/故事点录入 UI；Task 父子关系与状态流转
- [~] 工时：登记人归属/剩余工时扣减/按成员报表已完成；成本视图与迭代/日期过滤待补
- [ ] 原型：URL/图片/MCP 渲染、图片上传、版本历史、需求关联 UI、CSP 加固
- [ ] 模型供应商：默认供应商切换、模型手动增删改/启停、定时刷新

### P2 M3 工作流与卡点（Round 4 完成核心闭环）

- [x] `Workflow` / `WorkflowStep` 实体、迁移、CRUD API（Go 单步编排 + 模板实例化）
- [x] 审批策略：自动通过 / 仅卡点时询问 / 总是询问（每步可配，默认 BlockersOnly）
- [x] `Blocker` 卡点实体：原因、上下文快照、处理结果（继续/修改方向/取消）
- [x] 卡点队列页面：集中处理并续跑
- [x] `AgentRun` 关联工作流步骤；新增 `WaitingForHuman` 状态
- [~] `AgentTeam` / `AgentTeamMember` 实体 + 基础 API 已完成，团队指派 UI 待补
- [x] 内置三条模板流（需求交付/缺陷修复/发布）
- [x] 步骤失败自动重试；通过卡点处理实现单步重跑/改向
- [~] 前端：工作流面板（模板创建/启动/运行记录/运行时间线）+ 卡点队列页已完成；可视化步骤编辑器待补

### P3 M4 CI/CD（Round 5 完成主体）

- [x] GitHub / GitLab Token 加密存储（Project 级，DPAPI）
- [x] `gh` / `glab` 进程封装：PR/MR 创建、状态查询、合并、CI 状态、Release
- [x] 工作项 ↔ PR/MR 关联与 CI 状态回写（合并后自动流转 Done/Closed）
- [x] 发布流模板（Round 4）+ 直接发布 API（tag → Release）
- [x] 前端：集成配置卡、PR/CI 状态展示与同步、发布面板
- [ ] CI 状态实时推送（当前手动同步 / 轮询；SignalR 阶段补齐）

### P4 M5 打磨（Round 6 完成主体）

- [x] Skills：实体 + CRUD + Agent 装配 + 运行时注入系统提示词
- [ ] Skills 收尾：SKILL.md 目录/脚本/资源、导入导出、脚本执行
- [x] MCP：Server 配置、认证加密、启停、连通性测试、按 Agent 授权
- [ ] MCP 收尾：运行时 MCP 客户端（工具加载与调用）
- [x] 成本统计：token × 模型单价 → 项目级费用，按供应商/模型汇总
- [x] 审计日志：敏感操作留痕 + 查询 API/页面
- [ ] 结构化日志与可观测
- [ ] 安全加固：原型 CSP、密钥轮换界面、发布审批
- [ ] 部署文档 + `docker compose`

### P5 基建与质量

- [x] SignalR 实时推送（工作项状态 / AgentRun 进度 / 卡点，Round 7）
- [ ] PostgreSQL 支持（Npgsql + provider 切换 + 迁移验证）
- [x] CI（GitHub Actions：build/test/lint）+ Dockerfile + compose + 部署文档（Round 7）
- [ ] OpenAPI 生成前端客户端，删除手写类型
- [x] 前端测试（Vitest + Testing Library 起步，Round 8；Playwright E2E 待补）
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
- [ ] 关联原型 UI（`RequirementId` 无法在 UI 设置）
- [x] 详情页 / 编辑 UI / 指派人变更（Round 3）
- [x] 删除 UI（后端已有，Round 3）

### 4.3 缺陷管理（P0）

- [x] 创建/查询、严重级别、复现步骤、环境、状态机
- [x] 编辑缺陷字段（Round 3）
- [ ] 测试 Agent 验证后自动流转
- [ ] 关联需求 UI

### 4.4 原型工坊（P0）

- [x] HTML 编辑 + sandbox iframe 实时预览
- [ ] 多文件支持
- [ ] 版本历史（当前只有自增计数，无快照）
- [ ] URL / 图片 / MCP 原型渲染（当前只存字符串）
- [ ] 图片上传 / 存储
- [ ] 设计 MCP 客户端
- [ ] 原型 ↔ 需求关联 UI
- [ ] 严格 CSP 与沙箱加固

### 4.5 工时管理（P0）

- [x] 预估/已耗/剩余字段；手动登记；AgentRun 1:1 折算
- [x] 燃尽图、甘特图、工时报表（基础版）
- [x] 登记人归属（Round 3：Owner / Agent 成员）
- [x] 剩余工时自动扣减（Round 3）
- [ ] 成本视图（人力成本 + token 费用）
- [ ] 报表按迭代/日期过滤、估算偏差分析
- [ ] 甘特图按成员泳道汇总

### 4.6 任务与迭代看板（P0/P1）

- [x] 迭代 CRUD/开始/完成/删除、燃尽图
- [x] 看板拖拽流转（前端状态表 + 后端状态机）
- [ ] 迭代编辑 UI（后端 `PUT` 已有）
- [x] 工作项排入迭代的 UI（Round 3：编辑弹窗）
- [x] Task 拆解（父子关系）/ 独立流转（Round 3）
- [ ] 看板筛选（指派人/优先级/标签）、WIP 提示
- [ ] 列表视图 + 批量操作
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

- [ ] 唯一 Owner 账号 / 认证
- [x] 敏感操作审计日志（Round 6：删除/集成/发布等留痕 + 查询页）
- [x] 机密加密存储且不出现在响应（DPAPI）
- [ ] 密钥查看/轮换界面

### 5.1 模型供应商（P0）

- [x] 自定义 OpenAI 兼容端点 + DPAPI 加密 + 连通性测试
- [x] models.dev 目录同步（手动 + 定时刷新，失败保留缓存；Round 8）
- [x] 默认供应商切换（API + UI，Round 8）
- [x] 模型手动增删改/启停（API + UI，Round 8）
- [x] token 用量与费用汇总（Round 6：项目成本报表）
- [ ] 成本按迭代/日期维度过滤

### 5.2 Agent 定义（P0）

- [x] CRUD、岗位、模型、系统提示词、工具/命令白名单字段
- [x] 工具权限真正生效（Round 1）
- [ ] 提示词模板变量替换
- [ ] 沙箱策略字段
- [ ] 试运行可选需求并注入上下文

### 5.3 Agent 团队（P1）

- [x] 实体 + 基础 API（Round 4）
- [ ] 团队指派（项目/工作项类型默认团队）与前端管理 UI

### 5.4 工作流（P0 单步 → P1 编排）

- [x] 工作流定义/步骤/审批策略（Round 4）
- [x] 卡点机制（Round 4）
- [x] 三条内置模板（Round 4）
- [x] 实例化运行、失败重试、卡点续跑（Round 4；「全程自动驾驶」即默认策略）
- [x] 单步 AgentRun（手动指派/认领）
- [ ] 可视化步骤编排编辑器（当前由模板创建 / API 配置）

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
- [ ] 流式输出与工具调用级日志
- [ ] 多 Agent 协作
- [x] 沙箱加固（路径逃逸/命令注入/超时，Round 1）

### 5.7 MCP（P1）

- [x] Server 配置、认证加密、启停、连通性测试
- [x] 按 Agent 授权（AgentMcpServer）
- [ ] 运行时 MCP 客户端（握手/工具发现/调用）
- [ ] 设计 MCP 链路（原型 ↔ MCP）

### 5.8 单用户协同（P0）

- [x] 卡点队列（Round 4）
- [~] 审批策略默认 + 每步覆盖（每步已支持，全局默认配置 UI 待补）
- [x] 取消/接管入口（Round 1）
- [x] @Agent 触发（Round 2：评论提及 → AgentRun → 回复写回）
- [~] 项目概览（Round 3）与工作流运行面板；"看而不等"实时仪表盘待 M3 收尾

### 5.9 Agent 认领闭环（P0）

- [x] 指派 → 入队 → 执行 → 日志/产物/token → 工时 → PR 回填
- [ ] 看板卡片显示 Agent 头像/实时状态
- [x] 工作流完成时自动流转工作项状态（InProgress→InReview / Fixing→PendingVerification）
- [ ] Agent 作为项目成员/指派人
- [ ] PR 创建与合并（当前仅从输出正则抓链接）
- [ ] 失败重试

### 6. CI/CD（P1 · Round 5）

- [x] GitHub `gh` 封装（PR 创建/状态/合并/Release）
- [x] GitLab `glab` 封装（MR 创建/状态/合并/Release）
- [x] 发布流（模板 + 直接 Release API）
- [x] CI 状态回写（手动同步，合并自动流转）
- [ ] CI 状态实时推送（SignalR 阶段）

### 8. 架构与安全

- [x] 分层（Api → Agents/Infrastructure → Core）
- [x] ProblemDetails 错误处理
- [ ] SignalR
- [ ] PostgreSQL
- [x] 审计日志（Round 6）
- [ ] 认证
- [ ] CSP（原型）

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
| 13 | 前端手写 API 类型，未用 OpenAPI 生成 | `frontend/src/lib/types.ts` | P5 |
| 14 | 无 PostgreSQL provider，连接串切换无效 | `Program.cs` | P5 |
| 15 | 无认证 / 审计日志缺失 | 全局 | 审计 ✅ Round 6；认证 P5 |

## 4. 每轮工作日志

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
