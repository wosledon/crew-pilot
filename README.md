# Crew Pilot

> 一个人的项目驾驶舱 + 一队 AI 数字员工。产品需求见 [`docs/PRD.md`](docs/PRD.md)，工程约定见 [`AGENTS.md`](AGENTS.md)，P0/P1 验收见 [`docs/ACCEPTANCE.md`](docs/ACCEPTANCE.md)。

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
dotnet test                      # 运行全部测试（165 项）
cd frontend && npm run build     # 前端生产构建（tsc + vite）
cd frontend && npm run lint      # ESLint
cd frontend && npm run test:run  # 前端 Vitest（13 项）
cd frontend && npm run test:e2e  # Playwright E2E（自动启动 API + Vite）
cd frontend && npm run api:types # 由 docs/openapi.json 生成 API 类型

# 导出最新 OpenAPI（先启动 API），覆盖 docs/openapi.json
# Invoke-WebRequest http://localhost:5080/openapi/v1.json -OutFile docs/openapi.json

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

## 部署

- `docker compose up -d --build` → Web http://localhost:8081，API http://localhost:8080
- 详细说明（环境变量、手动部署、数据卷/备份、CI）见 [`docs/DEPLOY.md`](docs/DEPLOY.md)

## 当前进度（PRD P0/P1 全部完成；P2 后续演进见验收报告）

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
- [x] 测试：165 项后端 + 13 项前端（Vitest）+ 2 条 Playwright E2E，全部绿
- [x] 硬化：可取消的 AgentRun、工具白名单生效、路径沙箱、命令不经 shell、`LocalPath` 可配置
- [x] 协作：评论（@Agent 触发执行并回写）、附件上传下载、活动流时间线
- [x] 里程碑：CRUD + 迭代归属 + 甘特图标记
- [x] 工作项/项目：标签与编辑删除、Task 父子与流转、指派人变更、项目归档与成员管理、工时归属/剩余工时
- [x] M3 核心：工作流编排、审批策略、卡点队列、失败重试、三条内置模板、Agent 团队实体
- [x] M4 主体：项目 CI/CD 集成（Token 加密）、gh/glab PR/MR/CI/Release、合并自动流转、发布面板
- [x] M5 主体：Skills（CRUD/装配/运行时注入/SKILL.md 导入导出）、MCP 全传输与工具审计、Token/人力成本报表
- [x] OpenAPI：导出 spec、生成前端契约类型、核心 DTO 由契约派生、CI 校验生成结果
- [x] P5 部分：SignalR 实时推送、GitHub Actions CI、Docker/compose（SQLite/PostgreSQL 双 provider）、结构化 JSON 日志 + 关联 ID、前后端测试
- [x] 原型工坊：HTML 编辑 + 严格 CSP 沙箱预览、URL/图片/MCP 引用、图片上传、版本历史与恢复、需求关联
- [x] 工作项视图：卡片/列表切换、多选批量流转/指派/删除、看板标签筛选
- [x] M3：工作流可视化编辑器、Agent 团队管理、项目默认团队（按岗位解析 Agent）
- [x] 报表：工时/成本按迭代与日期过滤（甘特页过滤器）
- [x] E2E：Playwright 主链路（项目/工作项/看板）+ CI job；试运行支持注入工作项上下文
- [x] 安全：可选单用户密码认证（Cookie）、审计日志、DPAPI/AES-GCM 机密保护、原型 CSP
- [x] 看板 Agent 实时状态（最近 Run/进行中徽标）、缺陷↔需求关联
- [x] 报表：工时/成本按迭代与日期过滤、人力成本（工时×时薪）、估算偏差与超支项
- [x] 运行监控：执行记录详情（日志时间线/提示词/输出/耗时/token，运行中 2s 自动刷新）
- [x] M4：PR/MR 创建 + 同步 + 合并（gh/glab）、CI 状态回写与实时推送、发布流
- [x] 工作项：看板/列表/甘特/迭代编辑、标签/指派人/优先级筛选、WIP 提示、批量操作
- [x] 活动流按工作项/事件类型过滤；需求详情展示关联缺陷；README.en 英文说明`n- [x] 工作项类型级默认团队（需求/缺陷/任务，优先于项目默认）`n- [x] Agent 沙箱策略：工作区读写 / 只读 / 受信命令，工具权限联动`n- [x] Skills：SKILL.md 与 zip 资源包（scripts/resources）导入导出，路径穿越与大小校验`n- [x] 安全：Provider 密钥轮换（审计）、发布审批开关（直接发布需审批时 409）`n- [x] 甘特依赖 FS/SS + 关键路径高亮 + 按指派人工时泳道 + PM 周报`n- [x] 原型多文件（html/css/js 相对路径预览，CSP 与路径校验）`n- [x] CI Webhook 推送、Markdown 描述渲染、500 工作项规模化验证
