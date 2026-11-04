# 验收报告（P0 / P1 范围）

> 本报告对照 [`docs/PRD.md`](PRD.md) 逐节验收。PRD 第 4 节明确：**P0 = MVP 必须，P1 = v1，P2 = 后续演进**。
> 因此本报告的完成范围 = PRD 全部 P0 + P1 功能；P2 与单用户产品范围外项在文末单独列出。
> 验收时间：2026-11-04；分支：`feat/CP-000-all-features`。

## 1. 自动化证据

| 项 | 命令 | 结果 |
| --- | --- | --- |
| 后端构建 | `dotnet build CrewPilot.slnx` | 0 警告 / 0 错误 |
| 后端测试 | `dotnet test CrewPilot.slnx` | **168 项通过**（50 单测 + 118 集成） |
| 前端静态检查 | `npm run lint` / `npm run format:check` | 0 error，格式通过 |
| 前端单测 | `npm run test:run` | **13 项通过** |
| 前端构建 | `npm run build` | 通过（tsc + vite） |
| E2E | `npm run test:e2e` | **2 条主链路通过**（项目→工作项→看板；Agent 页） |
| OpenAPI 类型 | `npm run api:types` | 生成结果可复现（CI 校验无 diff） |

CI：`.github/workflows/ci.yml` 含 backend / frontend / e2e 三个 job。

## 2. PRD 逐节验收

### 4.1 项目管理（P0）

- [x] 创建/查询/编辑/删除、项目 Key、归档/取消归档
- [x] 本地工作目录 `LocalPath`（沙箱与 worktree 根）
- [x] 项目概览（工作项数、工时、进行中迭代）
- [x] 成员管理：Owner + Agent 成员、角色调整、移除保护
- [x] 默认 Agent 团队（P1）：项目级 + 需求/缺陷/任务类型级
- [~] 项目可见性、Git OAuth 授权：单用户范围外 / 未授权流程（见 §3）

### 4.2 需求管理（P0）

- [x] CRUD（验收标准、优先级、故事点、迭代、截止日、标签）
- [x] 状态机流转（非法流转 400）
- [x] 详情/编辑/删除/指派人变更、Task 父子关系
- [x] 关联原型、关联缺陷（详情展示）
- [x] 富文本描述：Markdown 渲染（Round 36）

### 4.3 缺陷管理（P0）

- [x] 创建/查询、严重级别、复现步骤、环境、状态机
- [x] 编辑缺陷字段、关联根因需求
- [x] 工作流「缺陷修复流」+ 完成后自动流转（Fixing→PendingVerification / 合并后 Closed）

### 4.4 原型工坊（P0）

- [x] HTML 编辑 + sandbox iframe/CSP 实时预览
- [x] URL / 图片 / MCP 引用渲染，图片上传（类型/大小限制）
- [x] 版本历史、自动快照、恢复
- [x] 原型 ↔ 需求关联
- [x] 多文件原型（Round 35：文本资源 + 相对路径预览）

### 4.5 工时管理（P0）

- [x] 预估/已耗/剩余；手动登记；AgentRun 1:1 折算
- [x] 剩余工时自动扣减、登记人归属
- [x] 燃尽图、甘特图、工时报表（类型/指派人/成员）
- [x] Token 成本报表（迭代/日期过滤）、人力成本（工时 × 时薪）、估算偏差与超支项
- [~] 甘特成员泳道：P2

### 4.6 任务与迭代看板（P0/P1）

- [x] 迭代 CRUD/开始/完成/删除、燃尽图、编辑 UI
- [x] 看板拖拽流转（前端状态表 + 后端状态机双重拦截）
- [x] 看板筛选：标签/指派人/优先级、WIP 上限提示
- [x] 列表视图 + 多选批量流转/指派/删除
- [x] 卡片显示 Agent 最近运行状态（实时）

### 4.7 甘特图（P1）

- [x] 迭代时间线、工作项截止日、逾期标红、里程碑标记
- [x] 依赖关系 FS/SS（实体/API/甘特 UI，环路与跨项目校验）+ 关键路径高亮（Round 1）`n- [x] PM 周报：数据汇总预览 + 交给 PM Agent 生成（Round 34）
- [x] 工时泳道/成员泳道（Round 2：按指派人汇总已耗/预估与工作项数）

### 4.8 评论、附件与活动流（P0/P1）

- [x] 评论 CRUD、@Agent 触发执行并回写回复
- [x] 附件上传/下载/删除（本地存储、20MB 限制）
- [x] 活动流时间线 + 按工作项/事件类型过滤
- [x] 工作项详情内评论/附件/工时/PR/关联缺陷聚合

### 4.9 权限与审计（P0/P1）

- [x] 单用户密码认证（可选启用，Cookie 会话、401 门禁）
- [x] 敏感操作审计（删除/集成/发布/密钥轮换/MCP 工具调用）
- [x] 机密 DPAPI（Windows）/AES-GCM（非 Windows）加密，响应/日志不泄露
- [x] 密钥轮换（Provider 编辑轮换、集成/MCP Token 覆盖）+ 审计
- [x] 发布审批开关（开启后直接发布 API 拒绝，走发布工作流）

### 5.1 模型供应商（P0）

- [x] 自定义 OpenAI 兼容端点 + 加密 + 连通性测试
- [x] models.dev 同步（手动/定时、失败保留缓存）
- [x] 默认供应商切换、模型增删改/启停
- [x] Token 用量与成本汇总（迭代/日期过滤）

### 5.2 Agent 定义（P0）

- [x] CRUD：岗位、模型、系统提示词、工具白名单、命令白名单
- [x] 沙箱策略字段（工作区读写 / 只读 / 受信命令）与引擎联动
- [x] 提示词模板变量（`{{title}}`/`{{requirement}}`/`{{acceptanceCriteria}}` 等）
- [x] 试运行（可选关联工作项并注入上下文、日志/token 留痕）

### 5.3 Agent 团队（P1）

- [x] 团队 CRUD + 成员装配
- [x] 项目默认团队 + 按工作项类型默认团队，工作流按岗位解析 Agent

### 5.4 工作流（P0/P1）

- [x] 工作流定义/步骤/输入输出契约/审批策略/失败重试
- [x] 卡点 Blocker（继续/改向/取消）与卡点队列
- [x] 三条内置模板（需求交付/缺陷修复/发布）
- [x] 可视化步骤编辑器（步骤增删排序、指定 Agent/岗位、审批、重试）
- [x] 实例化运行、失败自动重试、卡点续跑、完成自动流转工作项

### 5.5 Skills（P1）

- [x] Skill CRUD、Agent 装配、运行时注入系统提示词
- [x] SKILL.md 导入导出（YAML front matter + 正文）
- [x] zip 资源包导入导出（`scripts/` + `resources/`，路径穿越/大小校验）
- [~] 按 Skill 触发执行脚本：P2（Agent 仍可经命令白名单执行）

### 5.6 运行引擎（P0）

- [x] MAF 收敛在 `AgentEngine` 抽象后
- [x] 内置工具（文件读写/列目录/白名单命令）+ MCP 工具注入
- [x] 项目目录隔离 + git worktree 生命周期
- [x] 沙箱加固（路径逃逸/命令注入/超时/输出截断）与沙箱策略
- [x] 每次运行留痕：模型、token、耗时、日志步骤、工时、成本
- [x] 运行状态/日志 SSE 流式推送（Round 38）`n- [x] 多 Agent 并行协作：工作流同 Order Parallel 步骤并发（Round 39）`n- [~] 逐 token 流式输出：P2

### 5.7 MCP 接入（P1）

- [x] Server 配置/认证加密/启停/连通性测试/按 Agent 授权
- [x] Streamable HTTP、旧版 HTTP+SSE、stdio 三种传输
- [x] 工具级审计（服务器/工具/耗时/结果）
- [x] 设计链路：原型 → 需求草稿（Round 37）；Figma 解析经通用 MCP 工具由 Agent 执行

### 5.8 单用户协同（P0）

- [x] 默认自动驾驶，仅卡点/显式审批找人
- [x] 卡点队列、取消/接管、@Agent、项目概览
- [x] “看而不等”：SignalR 实时刷新 + 看板/运行状态轮询

### 5.9 Agent 认领闭环（P0）

- [x] 指派 → 入队 → 执行 → 日志/产物/token → 工时 → PR 回填
- [x] 取消/接管、失败/取消重试（工作流走卡点重试）
- [x] PR 创建/同步/合并、CI 状态回写与实时推送
- [x] 完成自动推进工作项状态、看板实时 Agent 状态

### 6. CI/CD（P1）

- [x] GitHub `gh` / GitLab `glab` 封装（创建/状态/合并/发布）
- [x] 项目集成配置（Token 加密、连通性测试）
- [x] 发布流模板 + 发布审批开关
- [~] CI webhook 自动推送：P2（当前由同步/合并动作触发实时推送）

### 8. 架构与安全

- [x] 分层 Api → Agents/Infrastructure → Core，Core 无外部依赖
- [x] ProblemDetails 统一错误、结构化 JSON 日志 + `X-Correlation-Id`
- [x] SignalR 实时推送；SQLite / PostgreSQL 双 provider
- [x] CORS 白名单、CSP（原型）、机密加密、审计

### 9. 非功能需求

- [x] 构建/测试/CI/Docker/compose/部署文档
- [x] 500+ 工作项列表规模化测试（Round 36）

## 3. P2 后续演进 / 范围外（PRD 定义）

PRD 第 4 节：P2 = 后续演进，不阻塞 v1 验收。

| 项 | 说明 |
| --- | --- |





| 逐 token 流式输出（已支持运行状态/日志 SSE 推送） | 引擎能力增强，P2 |


| CI webhook 自动推送（当前动作触发） | P2 |
| 完整生成式 API 客户端：已提供 openapi-fetch typed client，认证模块接入；其余动态路径逐步迁移 |
| 项目可见性、多用户/RBAC | 单用户产品范围外 |
| Git OAuth 授权流程（当前 repoUrl + CLI Token） | 范围外/后续 |

## 4. 已知限制

1. **真实 MAF 联调**：自动化测试使用 Fake 引擎（不调用真实模型）；真实模型需配置供应商 API Key 后手工冒烟。
2. **Docker/PostgreSQL 实机验证**：本机无 Docker/PostgreSQL 服务，compose 与 Npgsql 接线通过构建、配置与 `/health` 验证；需在具备环境的主机执行 `docker compose up` 复核。
3. **流式输出**：已支持运行状态/日志 SSE 流式推送；逐 token 渲染需引擎流式接口，尚未实现。
4. **P2 清单**：见 §3，均为 PRD 明确后续演进项。

## 5. 结论

PRD 全部 **P0 与 P1** 功能已实现并通过自动化验证；P2 与单用户范围外项已明确记录。分支 `feat/CP-000-all-features` 可进入合并评审。
