# Crew Pilot

> 一个人的项目驾驶舱 + 一队 AI 数字员工。产品需求见 [`docs/PRD.md`](docs/PRD.md)，工程约定见 [`AGENTS.md`](AGENTS.md)。

## 技术栈

| 领域         | 选型                                                                |
| ------------ | ------------------------------------------------------------------- |
| 后端         | .NET 10 ASP.NET Core Web API（EF Core + SQLite，可切 PostgreSQL）   |
| 前端         | React 18 + TypeScript + Vite + Tailwind CSS v4 + Radix/shadcn 模式  |
| Agent 运行时 | Microsoft Agent Framework（收敛在 `AgentEngine` 抽象之后，M2 落地） |
| 测试         | xUnit + FluentAssertions + `WebApplicationFactory`（内存 SQLite）   |

## 快速开始

### 1. 启动后端（API）

```bash
dotnet run --project src/CrewPilot.Api --launch-profile http
# 监听 http://localhost:5080
# 开发环境自动应用 EF 迁移并创建 crewpilot.db（已被 .gitignore 忽略）
# OpenAPI 文档：http://localhost:5080/openapi/v1.json
```

### 2. 启动前端

```bash
cd frontend
npm install
npm run dev
# 监听 http://localhost:5173，/api 请求自动代理到 5080
```

### 3. 常用命令

```bash
dotnet build CrewPilot.slnx      # 构建解决方案
dotnet test                      # 运行全部测试（24 项）
cd frontend && npm run build     # 前端生产构建（tsc + vite）
cd frontend && npm run lint      # ESLint（配置随脚手架完善）

# EF Core 迁移（Infrastructure 持迁移，Api 为启动项目）
dotnet ef migrations add <Name> -p src/CrewPilot.Infrastructure -s src/CrewPilot.Api
```

## 目录结构

```
src/
  CrewPilot.Api/            # 表现层：Controllers、DI 组装（薄）
  CrewPilot.Core/           # 领域层：实体、状态机（无外部依赖）
  CrewPilot.Infrastructure/ # EF Core、DPAPI 机密保护
  CrewPilot.Agents/         # AgentEngine 抽象（MAF 适配，M2）
frontend/                   # React + TS + Vite SPA
tests/
  CrewPilot.UnitTests/      # 状态机 / 工时单测
  CrewPilot.IntegrationTests/ # WebApplicationFactory API 集成测试
docs/PRD.md                 # 产品需求（事实来源）
```

## 当前进度（M0 + M1 + M2 已完成）

- [x] 解决方案分层（Api / Core / Infrastructure / Agents / Tests）
- [x] 项目 / 需求 / 缺陷 CRUD + 状态机 + 工时登记（1:1）
- [x] 原型工坊 v0：HTML 编辑 + 沙箱 iframe 实时运行
- [x] 前端骨架：i18n（zh-CN/en）、卡片风格、Radix 组件封装（禁原生控件）
- [x] 看板：dnd-kit 拖拽流转（前端状态表 + 后端状态机双重拦截）
- [x] 迭代管理：CRUD + 开始/完成 + 按工时燃尽图
- [x] 甘特图：迭代时间线 + 工作项截止日 + 逾期标红
- [x] 工时报表：按类型/指派人统计预估与已耗
- [x] 模型供应商：自定义 OpenAI 兼容端点 + DPAPI 加密 + models.dev 目录同步 + 连通性测试
- [x] Agent 数字员工：岗位/模型/提示词/工具权限 CRUD + 试运行
- [x] AgentEngine：Microsoft Agent Framework（MAF 1.27）落地，工具白名单 + worktree 隔离
- [x] 认领闭环：指派 → 后台执行 → 日志/产物/token 留痕 → 工时 1:1 折算 → PR 回填 → 可取消接管
- [x] 测试：47 项全绿（单测 + WebApplicationFactory 集成测试，Fake 引擎）
- [ ] M3：工作流编排、卡点机制、审批策略
- [ ] M4：gh/glab CI/CD 集成
- [ ] M5：Skills、设计 MCP、成本统计
