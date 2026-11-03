# Crew Pilot

> 一个人的项目驾驶舱 + 一队 AI 数字员工。产品需求见 [`docs/PRD.md`](docs/PRD.md)，工程约定见 [`AGENTS.md`](AGENTS.md)。

## 技术栈

| 领域 | 选型 |
| --- | --- |
| 后端 | .NET 10 ASP.NET Core Web API（EF Core + SQLite，可切 PostgreSQL） |
| 前端 | React 18 + TypeScript + Vite + Tailwind CSS v4 + Radix/shadcn 模式 |
| Agent 运行时 | Microsoft Agent Framework（收敛在 `AgentEngine` 抽象之后，M2 落地） |
| 测试 | xUnit + FluentAssertions + `WebApplicationFactory`（内存 SQLite） |

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

## 当前进度（M0 已完成）

- [x] 解决方案分层（Api / Core / Infrastructure / Agents / Tests）
- [x] 项目 / 需求 / 缺陷 CRUD + 状态机 + 工时登记（1:1）
- [x] 原型工坊 v0：HTML 编辑 + 沙箱 iframe 实时运行
- [x] 前端骨架：i18n（zh-CN/en）、卡片风格、Radix 组件封装（禁原生控件）
- [ ] M1：迭代、看板拖拽、甘特图（工时维度）、燃尽图
- [ ] M2：模型供应商（models.dev）、AgentEngine、Agent 认领闭环
- [ ] M3：工作流编排、卡点机制、审批策略
- [ ] M4：gh/glab CI/CD 集成
- [ ] M5：Skills、设计 MCP、成本统计
