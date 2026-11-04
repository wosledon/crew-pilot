# Crew Pilot

> A solo owner's project cockpit with a crew of AI digital employees.
> Product requirements: [`docs/PRD.md`](docs/PRD.md); engineering conventions: [`AGENTS.md`](AGENTS.md).

## Stack

| Area          | Choice                                                                 |
| ------------- | ---------------------------------------------------------------------- |
| Backend       | .NET 10 ASP.NET Core Web API (EF Core + SQLite, switchable PostgreSQL) |
| Frontend      | React 18 + TypeScript + Vite + Tailwind CSS v4 + Radix/shadcn pattern  |
| Agent runtime | Microsoft Agent Framework behind the `AgentEngine` abstraction         |
| Tests         | xUnit + FluentAssertions + `WebApplicationFactory` (in-memory SQLite)  |

## Quick start

### 1. Start the API

```bash
dotnet run --project src/CrewPilot.Api --launch-profile http
# http://localhost:5080
# Development auto-applies EF migrations and creates crewpilot.db (gitignored)
# OpenAPI: http://localhost:5080/openapi/v1.json
```

### 2. Start the web app

```bash
cd frontend
npm install
npm run dev
# http://localhost:5173, /api proxied to 5080
```

### 3. Common commands

```bash
dotnet build CrewPilot.slnx        # build solution
dotnet test                        # backend tests (168)
cd frontend && npm run build       # production build
cd frontend && npm run lint        # ESLint
cd frontend && npm run test:run    # Vitest (13)
cd frontend && npm run test:e2e    # Playwright E2E (starts API + Vite)
cd frontend && npm run api:types   # regenerate API types from docs/openapi.json

# EF Core migration (migrations live in Infrastructure)
dotnet ef migrations add <Name> -p src/CrewPilot.Infrastructure -s src/CrewPilot.Api
```

## Layout

```
src/
  CrewPilot.Api/            # Controllers, DI composition, middleware, SignalR
  CrewPilot.Core/           # Entities, state machines, domain services
  CrewPilot.Infrastructure/ # EF Core, secret protection, MCP clients, CI/CD CLI
  CrewPilot.Agents/         # AgentEngine abstraction (MAF adapter), workflow runner
frontend/                   # React + TS + Vite SPA
tests/
  CrewPilot.UnitTests/
  CrewPilot.IntegrationTests/
docs/PRD.md                 # source of truth
```

## Deployment

- `docker compose up -d --build` → web http://localhost:8081, API http://localhost:8080
- Environment variables, volumes/backup, PostgreSQL override and CI details: [`docs/DEPLOY.md`](docs/DEPLOY.md)
- Optional single-user authentication: set `Auth__Password` (cookie login).

## Feature status

- Projects / requirements / defects / tasks with state machines, tags, estimates, work logs
- Kanban (drag & drop, filters, WIP, live Agent status), list view with batch actions, Gantt, iterations
- Agent employees: providers & models (models.dev), skills (SKILL.md import/export), MCP servers (Streamable HTTP / legacy SSE / stdio + tool audit)
- AgentRun lifecycle incl. retry/cancel, worktree isolation, token & cost accounting, run detail timeline
- Workflows with per-step approval policies, blockers, teams (project default team), templates
- CI/CD: gh/glab PR create/sync/merge, CI status write-back & realtime push, releases
- Reports: hours, token cost, human cost, estimate deviation, burndown
- Prototype studio: HTML/URL/image/MCP references, CSP sandbox, image upload, versions
- Single-user auth (optional), audit log, encrypted secrets, structured JSON logs
- Tests: 168 backend, 13 Vitest, 2 Playwright E2E; OpenAPI-generated frontend DTOs
