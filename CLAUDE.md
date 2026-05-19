# TAIM — Team AI Manager

Autonomous multi-agent platform. User submits a goal → LLM bootstrap creates an executive AI team → each agent runs a kickoff (proposes KPIs, writes strategy report, dispatches delegations as work-item Actions) → results visible in the web UI.

## Current Build State (as of Sprint 6)

| Feature | Status |
|---|---|
| Login → submit goal → team assembly → executive kickoff | ✅ Complete |
| KPIs proposed and saved per agent | ✅ Complete |
| Kickoff strategy reports (stored + streamed via SignalR) | ✅ Complete |
| Approval queue (tool gates, UI + backend) | ✅ Complete |
| Real-time activity feed (SignalR + ActivityFeedChannel) | ✅ Complete |
| Actions table + API (work-item tracking) | ✅ Sprint 1 |
| Delegation dispatch (kickoff delegations → Action records) | ✅ Sprint 1 |
| Meetings (agent-to-agent coordination) | ✅ Sprint 2 |
| Agent work loop (execute actions, tool use) | ✅ Sprint 3 |
| Developer agents with ClaudeCode connector | ✅ Sprint 4 |
| KPI dashboard page | ✅ Sprint 5 |
| Approval audit trail + agent name fix | ✅ Sprint 5 |
| Action re-trigger (Run button) | ✅ Sprint 5 |
| Task termination (S6-001) | ✅ Sprint 6 |
| System-wide emergency stop (S6-002) | ✅ Sprint 6 |
| Agent scheduler / auto-retrigger (S6-003) | ✅ Sprint 6 |
| Sub-team spawning | 🔲 Sprint 7+ |

## Stack

| Layer | Technology |
|---|---|
| Backend | .NET 10, ASP.NET Core Minimal APIs, SignalR, EF Core 10 |
| Frontend | React 19, Vite, TypeScript, react-router-dom 7 |
| Database | PostgreSQL 16 (pgvector, pg_trgm, RLS) |
| Cache | Redis 7 |
| AI | Anthropic Claude (via OpenAI compat layer), OpenAI, Google Gemini, Ollama |
| MCP | stdio-based MCP servers (web-search, email, github) |

## Key Commands

```bash
./start.sh                          # build + start all Docker services
docker compose build taim-api       # rebuild only the API
docker compose up -d                # start stack in background
docker compose logs -f taim-api     # tail API logs
cd src/backend && dotnet build Taim.slnx
cd src/backend && dotnet test Taim.E2ETests/Taim.E2ETests.csproj
cd src/frontend/taim-web && npm run dev   # Vite dev server (port 5173)
```

## Access

- Web UI: http://localhost:3000
- API: http://localhost:5000 (internal; proxied by nginx)
- Default user: `admin@taim.local` / `taim-admin`

## Ports

| Port | Service |
|---|---|
| 3000 | taim-web (nginx → React SPA + proxy to API) |
| 5000 | taim-api (ASP.NET Core) |
| 5432 | PostgreSQL |
| 6379 | Redis |

## Architecture Flow

```
User submits goal
  → POST /api/tasks
  → Background Task.Run:
      1. ExpertAgent.GatherKnowledgeAsync (Anthropic)
      2. BootstrapAgent.RecommendTeamAsync (Anthropic) → TeamRecommendation
      3. AgentFactory.CreateFromRecommendationAsync → executive agents in DB
      4. AgentOrchestrator.KickoffTeamAsync → parallel per-agent:
           ProposeKpisAsync → save KPIs
           RunAsync (kickoff prompt) → ExecutiveResponse
           Save ExecutiveReportEntity → push SignalR ExecutiveReport notification
           Dispatch response.Delegations → Action records via IActionService
      5. task.status = "active"
  → Frontend TeamView polls/streams → shows team + status
  → Frontend Reports page fetches GET /api/reports?taskId= + streams new reports
```

## Where Things Live

```
src/backend/
  Taim.Core/          — domain interfaces + models (no dependencies)
    Actions/          — IActionService, ActionRecord, CreateActionRequest, UpdateActionRequest
    Agents/           — IAgentRegistry, AgentDefinition, AgentRole, AgentStatus
    KPIs/             — IKpiService, KpiNode, CreateKpiRequest
    Notifications/    — INotificationService, INotificationChannel, NotificationKind
    Reports/          — IReportService, ExecutiveReportRecord
    Approvals/        — IApprovalService
    Meetings/         — IMeetingStore
    Budget/           — IBudgetService
    Teams/            — ITaskService
  Taim.Data/          — EF Core DbContext, PostgreSQL services (ActionService, KpiService, etc.)
  Taim.Agents/        — Bootstrap, Expert, Executive agents; AgentOrchestrator (kickoff + delegation dispatch)
  Taim.Memory/        — KpiContextProvider, TeamContextProvider, ChatHistoryProvider, VectorMemory
  Taim.Api/           — Minimal API endpoints, SignalR hub, middleware
  Taim.Providers/     — IChatClient factories (Anthropic, OpenAI, Google, Ollama)
  Taim.Budget/        — TokenLedgerMiddleware wraps IChatClient for spend tracking
  Taim.Notifications/ — NotificationService + ActivityFeedChannel (in-memory activity log)
  Taim.Connectors/    — McpStdioConnector spawns MCP server subprocesses (ClaudeCode, WebSearch)
  Taim.Workflows/     — Durable task scaffolding (future use)
  Taim.Tests/         — HTTP smoke tests (require live Docker stack)
  Taim.E2ETests/      — Playwright + real-stack integration tests (see src/ui-tests/)

src/frontend/taim-web/src/
  lib/api.ts          — all backend HTTP calls (relative URLs, auth header)
  lib/signalr.ts      — SignalR connection + onNotification()
  lib/types.ts        — shared TypeScript types
  features/           — page-level components (task-intake, team-view, reports…)
  components/         — reusable UI components

mcp-servers/          — web-search, email, github (Node.js stdio servers)
infra/                — docker-compose.yml, init.sql, nginx config
```

## Process & Documentation

| File | Purpose |
|---|---|
| `PROCESS.md` | **Read this after CLAUDE.md.** Feature lifecycle, invariants, sprint plan, roles |
| `METRICS.md` | Platform health, sprint progress, test counts, open bugs |
| `specs/` | Feature specs (`sprint-N/`) and bug reports (`bugs/`) |
| `specs/_template.md` | Template for new feature specs |
| `docs/user-guide.md` | End-user guide (updated per sprint) |
| `.claude/commands/` | Skill files: `/implement-feature`, `/new-feature`, `/new-bug`, `/review-feature`, `/run-tests`, `/update-metrics` |

## Critical Invariants

- **DI scoping**: `TaimDbContext` = Scoped (NOT pool). `RlsInterceptor` = Scoped. `ProviderFactory` = Scoped. Background work uses `IServiceScopeFactory` to create fresh scopes.
- **RLS**: `RlsInterceptor` sets `app.tenant_id` on every PostgreSQL connection. All queries are tenant-filtered automatically.
- **Enum serialization (HTTP)**: `JsonStringEnumConverter(CamelCase)` in `Program.cs` → `AgentRole.Ceo` → `"ceo"`, `AgentRole.ProductManager` → `"productManager"`.
- **Enum serialization (SignalR)**: `JsonStringEnumConverter(SnakeCaseLower)` → `NotificationKind.ExecutiveReport` → `"executive_report"`.
- **No `ChatResponseFormat.Json`**: Anthropic's OpenAI compat layer rejects it. Use `AgentJson.Deserialize<T>()` which handles markdown fences and snake_case key normalization.
- **Executive agents are NOT in DI**: `CeoAgent`, `CtoAgent`, etc. are instantiated directly (`new CeoAgent(chatClient)`) in `AgentFactory` and `AgentOrchestrator`.
- **EF column mapping**: explicit `.HasColumnName()` for every property. No snake_case convention. See `TaimDbContext.cs`.
- **MCP servers**: stdio transport only — NOT standalone services. `McpStdioConnector` spawns them as subprocesses from the API container.
