# 部署指南

Crew Pilot 是单用户自托管应用：一个 ASP.NET Core API + 一个 React 静态站点 + SQLite 数据目录。
推荐 `docker compose` 一键启动；也可手动部署。

## 1. docker compose（推荐）

前置：Docker Desktop / Docker Engine + Compose v2。

```bash
docker compose up -d --build
```

启动后：

| 服务 | 地址 | 说明 |
| --- | --- | --- |
| Web | http://localhost:8081 | React SPA（nginx 静态托管 + 反代） |
| API | http://localhost:8080 | ASP.NET Core（OpenAPI 仅开发环境） |

- SQLite 与附件数据保存在命名卷 `crewpilot-data`（容器内 `/app/App_Data`）。
- 首次启动自动建库（`Database__AutoMigrate=true`）。
- 升级：`docker compose pull && docker compose up -d --build`，数据卷保留。
- 备份：`docker run --rm -v crewpilot_crewpilot-data:/data -v ${PWD}:/backup alpine tar czf /backup/crewpilot-backup.tgz -C /data .`

### 环境变量（api 服务）

| 变量 | 默认 | 说明 |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | 运行环境；`Development` 时额外暴露 OpenAPI |
| `ConnectionStrings__CrewPilot` | `Data Source=/app/App_Data/crewpilot.db` | 数据库连接串 |
| `Database__AutoMigrate` | `true`（容器内） | 启动时自动迁移 |
| `Attachments__RootPath` | `/app/App_Data/attachments` | 附件存储目录 |

## 2. 手动部署（Windows / Linux）

后端：

```bash
dotnet publish src/CrewPilot.Api/CrewPilot.Api.csproj -c Release -o out
cd out
ASPNETCORE_URLS=http://0.0.0.0:5080 \
ASPNETCORE_ENVIRONMENT=Production \
Database__AutoMigrate=true \
dotnet CrewPilot.Api.dll
```

前端：

```bash
cd frontend
npm ci
npm run build          # 产物 frontend/dist
```

将 `frontend/dist` 交给任意静态服务器（nginx/Caddy/IIS），并把 `/api` 与 `/hubs` 反代到后端。
nginx 参考配置见 `frontend/nginx.conf`。

> 前端与 API 同源部署可避免 CORS；若跨域，需在 `Program.cs` 的 `FrontendDev` 策略中补充来源。

## 3. 数据与进程隔离

- Agent 执行依赖 `Project.LocalPath` 指向的本地 git 仓库；容器化部署时需将仓库目录挂载进 api 容器，并保证 `git` / `gh` / `glab` 可用。
- `gh` / `glab` 在容器内默认未安装；如使用 M4 CI/CD，请在镜像中安装 CLI 或改为在宿主机运行 API。
- 每个开发 AgentRun 会在工作项独立 git worktree 中执行，结束时清理（成功保留分支/PR）。

## 4. PostgreSQL（规划中）

PRD 目标为生产 PostgreSQL、开发 SQLite。当前迁移脚本按 SQLite 生成；切换 provider 需补充 PostgreSQL 迁移与验证，尚未纳入本指南。

## 5. CI

GitHub Actions：`.github/workflows/ci.yml`

- backend：`dotnet restore/build/test`（Release）
- frontend：`npm ci` + `lint` + `format:check` + `build`

## 6. 常见问题

| 现象 | 处理 |
| --- | --- |
| 8081 页面空白 | 检查 `web` 容器日志；确认 nginx 反代 `api` 名称可解析 |
| API 500 / 数据库未创建 | 确认 `Database__AutoMigrate=true`，或手动执行迁移 |
| Agent 执行失败提示没有 git | 容器内安装 git，并确认仓库路径已挂载 |
| 实时事件不刷新 | 检查 `/hubs/realtime` WebSocket 升级（nginx `Upgrade` 头） |
