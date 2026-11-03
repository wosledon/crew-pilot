# AGENTS.md — Crew Pilot 项目约定

> 本文件是 AI 编程助手与人类开发者在本仓库工作的**统一约定**。产品需求以 `docs/PRD.md` 为唯一事实来源；本文件规定"怎么写"，PRD 规定"做什么"。两者冲突时，先更新 PRD 再改代码。

## 1. 项目概述

Crew Pilot = **一个人的项目驾驶舱 + 一队 AI 数字员工**。面向单一负责人（Solo Owner）：人类只做决策和处理卡点，产品/架构/开发/测试/项目经理/运维六个岗位由 Agent 员工执行，工作流默认自动驾驶，仅在"卡点"时挂起找人。

三大产品原则（写代码时不可违背）：

1. **留痕**：每次 AgentRun 必须有完整日志、产物、工时与 token 成本；
2. **可接管**：任何 Agent 工作项，用户可在任意时刻切回人工；
3. **默认自动**：流程默认全自动，审批仅在卡点或用户显式配置"总是询问"时发生。

## 2. 技术栈与关键决策（已定，勿再讨论）

| 领域           | 决策                                                                                                  |
| -------------- | ----------------------------------------------------------------------------------------------------- |
| 后端           | .NET 10 ASP.NET Core Web API（`net10.0`），OpenAPI，EF Core，SignalR                                  |
| 前端           | React + TypeScript + Vite                                                                             |
| Agent 运行时   | Microsoft Agent Framework（MAF），**必须收敛在 `AgentEngine` 抽象之后**，禁止业务代码直接依赖框架 API |
| 数据存储       | 开发 SQLite / 生产 PostgreSQL（连接串切换）                                                           |
| 模型来源       | 仅自定义供应商（OpenAI 兼容端点）+ models.dev；**不做本地模型（Ollama 等）**                          |
| Agent 执行环境 | 项目目录隔离 + 本地开发沙箱；开发 Agent 按工作项创建独立 git worktree 作业，完成后合并/提 PR 并清理   |
| 审批基调       | 全局默认"仅卡点时询问"                                                                                |
| 工时折算       | AgentRun 耗时 1:1 折算为工时，不加权                                                                  |
| 部署           | 单用户自托管（主线），`docker compose` 为可选方式                                                     |
| 原型           | 内置 HTML 原型工坊（沙箱 iframe 直跑）+ 设计 MCP 对接，来源不限（HTML/URL/图片/MCP）                  |

## 3. 目录结构与分层

目标结构（按 M0 里程碑推进，当前 `src/CrewPilot.Api` 为模板状态）：

```
src/
  CrewPilot.Api/            # 表现层：Controllers、SignalR Hub、中间件、DI 组装（薄）
  CrewPilot.Core/           # 领域层：实体、领域服务、状态机、工作流引擎（无外部依赖）
  CrewPilot.Infrastructure/ # 基础设施：EF Core、加解密、进程执行、gh/glab、MCP 客户端
  CrewPilot.Agents/         # Agent 平台：AgentEngine 抽象与 MAF 适配、Skills、AgentRun
frontend/                   # React + TS + Vite SPA
tests/
  CrewPilot.UnitTests/
  CrewPilot.IntegrationTests/
docs/PRD.md                 # 产品需求文档（事实来源）
```

约定：

- 依赖方向单向：`Api → Agents/Infrastructure → Core`，**Core 不依赖任何外层**；
- Controllers 只做参数校验与响应组装，业务规则落在 Core 领域服务；
- 跨模块调用通过接口，禁止直接 `new` 基础设施实现；
- 模板残留（`WeatherForecast*`）在首个真实功能落地时移除。

## 4. 后端约定（C# / .NET 10）

- 项目设置保持：`Nullable=enable`、`ImplicitUsings=enable`；
- 使用文件范围命名空间、`record` 定义 DTO/命令、主构造函数注入；
- 异步方法一律接受并传递 `CancellationToken`；
- 私有字段 `_camelCase`，公共成员 `PascalCase`，路由用 kebab-case，JSON 序列化用 camelCase；
- 错误处理：全局异常中间件 + `ProblemDetails`，不在响应里泄露堆栈；
- EF Core：显式迁移（不放自动迁移），查询默认 `AsNoTracking()`，禁用延迟加载，`DbContext` 生命周期为 Scoped；
- 机密（API key、token、MCP 认证）静态加密（DPAPI/AES-GCM），**永不出现在响应与日志**；
- 进程执行（gh/glab/终端）必须走统一封装：白名单 + 超时 + 输出截断 + 审计；
- 工作项状态流转必须走领域状态机（`待梳理→已就绪→进行中→待评审→已完成` 等），禁止在 Controller 里直接改状态。

## 5. 前端约定（frontend/）

### 5.1 技术选型（已定）

- React + TypeScript + Vite，`strict` 模式开启，**禁止 `any`**（用 `unknown` + 收窄）；
- 样式：Tailwind CSS + CSS 变量设计令牌；组件基于 Radix 原语封装（shadcn/ui 模式）；
- 图标：lucide-react；表单：react-hook-form + zod；服务端状态：TanStack Query；轻量客户端状态：Zustand（不引 Redux）；
- 表格：TanStack Table；看板拖拽：dnd-kit；甘特图：自绘或轻量库（M1 前定稿）；
- 国际化：i18next + react-i18next（见 5.4）；轻提示：sonner（或等价方案）；
- 原型工坊：Monaco/CodeMirror 编辑器 + 沙箱 iframe 分屏实时预览；
- ESLint + Prettier 强制通过后才可提交；路径别名 `@/` 指向 `src/`；
- API 客户端由 OpenAPI 生成，**禁止手写与后端重复的类型**。

### 5.2 代码风格

- 只用函数组件 + Hooks，禁 class 组件；
- 组件文件 `PascalCase.tsx`，工具/Hook `camelCase.ts`（Hook 以 `use` 开头）；
- 组件**命名导出**（禁默认导出），Props 类型命名 `XxxProps`；
- 一个文件一个组件；超过 ~150 行或复用时拆出；组件按 feature 目录聚合（`features/kanban/`、`features/prototype/` 等）；
- 副作用只在 Hook 内；数据请求一律 TanStack Query，不在组件里裸 `fetch`；
- 用户可见文案一律走 i18n（见 5.4），默认简体中文；代码注释中文即可，说明"为什么"而非复述代码。

### 5.3 组件化与交互规范

**禁止使用浏览器原生交互控件**，一律用封装好的 React 组件：

| 场景     | 禁止                                  | 必须使用                                                                |
| -------- | ------------------------------------- | ----------------------------------------------------------------------- |
| 下拉选择 | 原生 `<select>`                       | `components/ui/Select`（Radix Select 封装）                             |
| 下拉菜单 | 原生 `<menu>` / 手拼右键菜单          | `components/ui/DropdownMenu`（Radix 封装）                              |
| 文字提示 | `title` 属性                          | `components/ui/Tooltip`（Radix Tooltip 封装）                           |
| 确认框   | `window.confirm` / `alert` / `prompt` | `components/ui/ConfirmDialog`（Radix AlertDialog 封装，全仓库统一入口） |
| 轻提示   | `alert` / 各处自绘                    | `components/ui/Toast`（sonner 封装）                                    |
| 弹层     | 自写定位逻辑                          | Radix Popover / Dialog / Sheet 封装                                     |

组件分层：

- `components/ui/`：通用原语封装（Button、Select、Tooltip、ConfirmDialog、Toast……），**全仓库复用，禁止各 feature 各写一套**；
- `features/<feature>/components/`：业务组件，只准组合 `components/ui/` 与 hooks，不准再直接包原生控件；
- 组合优先：`children` 插槽 > 配置对象轰炸；Props 接口命名 `XxxProps`，可选 props 给默认值；
- 所有弹层类组件必须满足：Esc 关闭、点击外部关闭、焦点圈定、开关有动画、`z-index` 走令牌统一管理层级；
- 破坏性操作（删除、取消 AgentRun、放弃接管等）必须经 `ConfirmDialog` 二次确认，danger 样式 + 明确后果描述。

### 5.4 国际化（i18n）

- 技术：i18next + react-i18next；语言包为 JSON，按 feature 分 namespace，存放于 `frontend/src/i18n/locales/{zh-CN,en}/`；
- 默认语言**简体中文（zh-CN）**，首次跟随浏览器语言，用户可在设置中切换并持久化（localStorage）；
- **所有用户可见文案必须走 `t()`**，组件、测试中禁止硬编码任何语言的字面量（含占位符、`aria-label`、错误提示）；
- key 命名 `feature.section.item`（如 `kanban.card.move`），语义化，禁止拼音与中英混杂；
- 句式用插值（`t('x', { count })`），禁止字符串拼接造句；
- 日期、时间、数字、货币统一走 `Intl` API 或 i18next formatter，不手写格式；
- 新增/修改文案时**必须同步补齐所有语言文件**；语言包 key 不一致视为 lint 失败（后续在 CI 落地校验）。

### 5.5 视觉与 UI 风格

- 布局：左侧固定导航 + 主内容区；**卡片是本产品的核心视觉语言**——列表项、面板、详情分区、空状态、统计块一律用卡片承载；
- 卡片令牌统一：`rounded-lg` + 1px 细边框 + 轻投影（shadow-sm） + 内边距 16/24px 两档；hover 时边框加深、轻微抬升；卡片网格响应式（`grid` + `auto-fill minmax`）；
- 设计令牌经 CSS 变量定义于 `frontend/src/styles/tokens.css`：中性灰阶（slate）+ 单一主色（indigo，呼应"领航"主题），浅色/深色两套，`class` 策略切换、默认跟随系统；
- 间距按 4px 网格（Tailwind 默认刻度），圆角 `rounded-md/lg`，字号基准 14px，中文优先字体栈（PingFang SC / Microsoft YaHei / system-ui）；
- 状态色语义化：成功=emerald、警告=amber、危险=rose、信息=sky；Agent 相关元素用统一视觉标识（如 Agent 头像/徽标），与人类用户可区分；
- 交互反馈：所有异步操作有 loading 态；破坏性操作经统一 `ConfirmDialog` 二次确认；表单即时校验（zod 错误经 i18n 中文展示）；
- 可访问性：键盘可达、焦点可见、对比度 ≥ 4.5:1、图标按钮必须有 `aria-label`；
- 空状态、错误状态、加载态是每个视图的必备三件套，不留裸白屏。

## 6. Agent 平台专项约定（本仓库的"元规则"）

AI 助手在本仓库改代码时同样适用：

- 所有对 MAF 的调用只允许出现在 `CrewPilot.Agents/AgentEngine` 适配层；
- 不得为了跑通功能而跳过卡点机制或审批策略（测试场景使用 Fake Provider 除外，且需在测试名中标注）；
- 不得在代码、注释、提交信息中写入任何真实密钥；
- 开发 Agent 改动代码必须落在对应工作项的 git worktree，不直接改主工作区（见第 7 节）；
- 涉及状态机、工时折算、审批策略的改动必须同步更新对应单元测试。

## 7. Git 与提交约定

- 主分支 `main` 受保护，不直接提交；
- 分支命名：`feat/CP-<工作项编号>-<简述>`、`fix/...`、`chore/...`、`docs/...`；
- 提交信息用 Conventional Commits：`<type>(<scope>): <中文简述>`，type ∈ `feat/fix/refactor/test/docs/chore/perf`，scope 如 `api/core/agents/web/ci`；
- 每个提交保持可构建、可测试；提交前本地 `dotnet build` + `dotnet test` + 前端 `lint` 全绿；
- Agent 生成的代码经 worktree + PR 合入，PR 描述写明对应工作项与验收标准。

## 8. 测试约定

- 框架：xUnit + FluentAssertions；命名 `ClassName_方法_场景`（或中文场景描述）；
- 结构 Arrange-Act-Assert；一个测试只断言一类行为；
- 集成测试：`WebApplicationFactory` + SQLite 内存库覆盖核心 API；不依赖真实模型——Agent 引擎测试使用 Fake LLM Provider；
- 状态机、审批策略、工时 1:1 折算为核心测试区，改动必配测试。

## 9. 常用命令

```bash
# 解决方案：CrewPilot.slnx
dotnet build CrewPilot.slnx
dotnet test
dotnet run --project src/CrewPilot.Api            # 启动 API（开发环境自动挂 OpenAPI）

# EF Core 迁移（Infrastructure 持迁移，Api 为启动项目）
dotnet ef migrations add <Name> -p src/CrewPilot.Infrastructure -s src/CrewPilot.Api

# 前端（frontend/ 初始化后）
cd frontend
npm install
npm run dev        # 开发服务器
npm run build      # 生产构建
npm run lint       # ESLint 检查
```

## 10. 决策变更流程

本文件与 PRD 中的"已定"决策如需调整：先改 `docs/PRD.md`（§11.2 关键决策表），再同步本文件，最后动代码——不允许代码先行、文档补票。
