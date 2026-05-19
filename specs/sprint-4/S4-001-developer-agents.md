---
id: S4-001
title: Developer Agents and Operational Tool Infrastructure
sprint: 4
status: done
created: 2026-05-18
updated: 2026-05-18
---

# S4-001 — Developer Agents and Operational Tool Infrastructure

## Problem Statement

Three things prevent any agent from doing real work after Sprint 3. First, Developer and QA agents run the executive kickoff algorithm — they produce strategy reports and propose KPIs, which is wrong; they should activate silently and wait for action assignments. Second, the `taim-api` Docker runtime image (`aspnet:10.0-alpine`) has no `node` binary and no `claude` CLI, so `WebSearchConnector` and `ClaudeCodeConnector` crash the moment any agent tries to invoke them. Third, `ActionWorker` gates every tool call — including every web search — on user approval; the first search will immediately block the agent and stall the loop. All three must be fixed before the self-build test is meaningful.

## Solution Overview

**Worker agent kickoff**: Add a `WorkerAgentBase` class and a `WorkerKickoffAsync` path in `AgentOrchestrator`. Worker roles (Developer, QA, Designer, etc.) activate, log "ready", and go idle without an LLM call. No strategy report, no KPIs, no delegation.

**Pre-seeded web_search approval**: `AgentFactory.CreateFromSpecAsync` inserts a long-lived `agent_and_tool` approval for `web-search` immediately after creating each agent. This means web searches never hit the approval queue. Only `claude_code` (code writing) requires explicit user approval — the appropriate gate for a destructive action.

**Connector infrastructure**: The Dockerfile gains Node.js, the `claude` CLI, and the MCP server source files. `docker-compose.yml` adds a `workspaces-data` volume and wires `BRAVE_API_KEY`. `ClaudeCodeConnector` reads a `Workspace:Root` config key (default `/app/workspaces`) so agents have a stable working directory.

After this sprint, submitting a coding goal produces a team where executive agents search the web to inform their strategy, and Developer agents receive `claude_code` as a tool and execute code-writing actions (gated on the user approving the first code-write per agent).

## Data Model

No new tables. One new row inserted into `approvals` per agent creation (via `IApprovalService.CreateAsync` with `ApprovalScope.AgentAndTool`).

### Schema note

The `approvals` table must allow `decided_at` to be NULL for pre-seeded approvals that are already `approved`. This is currently already the case — `decided_at` defaults to null and is set only on the `ApplyDecisionAsync` path. Pre-seeded approvals skip that path and are inserted directly as `status=approved, scope=agent_and_tool`.

`IApprovalService` needs a new method to support bulk pre-seeding without triggering the `ApprovalRequired` notification:

```csharp
// New method in IApprovalService (Taim.Core/Approvals/ApprovalModels.cs):
Task PreApproveAsync(Guid tenantId, Guid agentId, string toolName,
    ApprovalScope scope = ApprovalScope.AgentAndTool,
    CancellationToken ct = default);
```

Implementation in `ApprovalService`: insert directly with `status="approved"`, `scope="agent_and_tool"`, no notification.

## Core Interface Changes

### `Taim.Core/Approvals/ApprovalModels.cs`

Add `PreApproveAsync` to `IApprovalService`:

```csharp
Task PreApproveAsync(Guid tenantId, Guid agentId, string toolName,
    ApprovalScope scope = ApprovalScope.AgentAndTool,
    CancellationToken ct = default);
```

### `Taim.Agents/Worker/WorkerAgentBase.cs` (new)

Simple agent for non-executive roles. No JSON parsing needed — kickoff is a log statement, no LLM call.

```csharp
public sealed class WorkerAgentBase(string roleName)
{
    public string RoleName => roleName;
    // No RunAsync / ProposeKpisAsync — not used in worker kickoff
}
```

### `AgentOrchestrator` changes

Add `IsWorkerRole(AgentRole)` predicate:

```csharp
private static bool IsWorkerRole(AgentRole role) => role switch
{
    AgentRole.Ceo or AgentRole.Cto or AgentRole.Cmo
    or AgentRole.Cfo or AgentRole.Hr => false,
    _ => true
};
```

Split `KickoffAgentAsync` into:
- Existing path (executive): ProposeKpisAsync + RunAsync + report + delegation dispatch
- New `WorkerKickoffAsync` path: status→Active, log "ready", status→Idle. No LLM call.

In `SafeKickoffAsync`, route on `IsWorkerRole(definition.Role)`.

### `AgentFactory.CreateFromSpecAsync` changes

After the agent DB record is created and before returning, call:
```csharp
await approvalService.PreApproveAsync(tenantId, agent.Id, "web-search", ct: ct);
```

This requires `IApprovalService` injected into `AgentFactory`. Check if it's already present; if not, add it.

## Docker Infrastructure

### Dockerfile (`src/backend/Taim.Api/Dockerfile`)

The runtime stage must change from `aspnet:10.0-alpine` to a Debian-based image that can have Node.js installed alongside .NET. Also, the build context is `./src/backend` which does not include `mcp-servers/`. The build context must be changed to the repo root.

**Required changes:**

1. **`docker-compose.yml` build context**: change from `context: ./src/backend` to `context: .` (repo root) for `taim-api`. Update `dockerfile` path to `src/backend/Taim.Api/Dockerfile`.

2. **Dockerfile multi-stage update:**

```dockerfile
# Stage 1 — .NET build (context is now repo root)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src
COPY src/backend/Taim.slnx ./
COPY src/backend/Taim.Core/ Taim.Core/
COPY src/backend/Taim.Data/ Taim.Data/
COPY src/backend/Taim.Agents/ Taim.Agents/
COPY src/backend/Taim.Memory/ Taim.Memory/
COPY src/backend/Taim.Connectors/ Taim.Connectors/
COPY src/backend/Taim.Budget/ Taim.Budget/
COPY src/backend/Taim.Notifications/ Taim.Notifications/
COPY src/backend/Taim.Providers/ Taim.Providers/
COPY src/backend/Taim.Workflows/ Taim.Workflows/
COPY src/backend/Taim.Api/ Taim.Api/
COPY src/backend/Taim.Host/ Taim.Host/
COPY src/backend/Taim.Tests/ Taim.Tests/
COPY src/backend/Taim.E2ETests/ Taim.E2ETests/
RUN dotnet restore Taim.slnx
RUN dotnet publish Taim.Api/Taim.Api.csproj -c Release -o /app/publish --no-restore

# Stage 2 — MCP server dependencies
FROM node:20-slim AS node-build
WORKDIR /mcp
COPY mcp-servers/web-search/ web-search/
COPY mcp-servers/email/ email/
RUN cd web-search && npm ci --omit=dev
RUN cd email && npm ci --omit=dev

# Stage 3 — Runtime: .NET + Node.js
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y --no-install-recommends nodejs && \
    npm install -g @anthropic-ai/claude-code && \
    apt-get purge -y curl && apt-get autoremove -y && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=dotnet-build /app/publish ./
COPY --from=node-build /mcp mcp-servers/
RUN mkdir -p /app/workspaces
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Taim.Api.dll"]
```

> **Implementation note**: If `@anthropic-ai/claude-code` is unavailable via npm at build time (e.g., rate limits, version changes), fall back to verifying `claude` is in PATH without the npm step and document it as a manual prerequisite. The acceptance criterion is `claude --version` succeeds inside the running container.

3. **`docker-compose.yml` additions for `taim-api`:**

```yaml
# Add to taim-api environment:
Brave__ApiKey: ${BRAVE_API_KEY:-}
Workspace__Root: /app/workspaces

# Add to taim-api volumes:
volumes:
  - workspaces-data:/app/workspaces

# Add to top-level volumes:
volumes:
  workspaces-data:
```

4. **`.env.example`** (create or update at repo root):

```
# Brave Search API key — get a free key at https://brave.com/search/api/
BRAVE_API_KEY=
```

### `ClaudeCodeConnector` workspace config

Update `ClaudeCodeConnector` to:
- Accept `IConfiguration` via constructor injection
- Read `Workspace:Root` (default `/app/workspaces`)
- Make `workingDirectory` parameter optional in `RunClaudeCode`; default to `_workspaceRoot`

```csharp
public sealed class ClaudeCodeConnector(IConfiguration config) : IConnector
{
    private readonly string _workspaceRoot =
        config["Workspace:Root"] ?? "/app/workspaces";
    // ...
    private async Task<string> RunClaudeCode(string prompt, string? workingDirectory = null)
    {
        var dir = workingDirectory ?? _workspaceRoot;
        // create dir if not exists
        Directory.CreateDirectory(dir);
        // ...existing process start logic...
    }
}
```

Since `ClaudeCodeConnector` is now registered as a singleton that takes `IConfiguration`, update `ConnectorExtensions.cs` to register it as a factory that resolves `IConfiguration` from DI:

```csharp
services.AddSingleton<IConnector>(sp =>
    new ClaudeCodeConnector(sp.GetRequiredService<IConfiguration>()));
```

## API Contract

No new endpoints. The existing `POST /api/actions/{id}/execute` from Sprint 3 is the re-trigger path after the user approves a `claude_code` call.

## UI/UX

No new pages. Two additions to the user guide:

1. **Worker agents in the team view**: Developer/QA agents show status `Idle` immediately after kickoff (not `Active` for long like exec agents). Their agent cards show `role: developer`, `status: idle`. They activate when the work loop starts.

2. **Approving claude_code**: When a Developer agent first attempts to write code, it will appear in the Approvals page requesting permission for `claude_code`. The user should approve "Agent + Tool (always)" to allow the agent to write code going forward. After approval, click "Re-run" (the `POST /api/actions/{id}/execute` endpoint) or the action will be re-triggered automatically on next kickoff.

```
Approvals page — pending claude_code request:
┌─────────────────────────────────────────────┐
│ PENDING APPROVAL                            │
│                                             │
│  Alex (Developer) wants to call:            │
│  Tool: claude_code                          │
│  Prompt: "Add unit tests for ActionService" │
│  Directory: /app/workspaces                 │
│                                             │
│  [Approve once]  [Always allow]  [Deny]     │
└─────────────────────────────────────────────┘
```

The "Always allow" button sets `scope = AgentAndTool` — the agent can write code from then on without further approvals.

## Implementation Order

Work through these in order; `dotnet build` after each backend change.

1. **`Taim.Core/Approvals/ApprovalModels.cs`** — add `PreApproveAsync` to `IApprovalService`
2. **`Taim.Data/Services/ApprovalService.cs`** — implement `PreApproveAsync` (direct insert, no notification)
3. **`Taim.Agents/Worker/WorkerAgentBase.cs`** — new minimal class
4. **`AgentOrchestrator.cs`** — add `IsWorkerRole`, `WorkerKickoffAsync`, route in `SafeKickoffAsync`
5. **`AgentFactory.cs`** — inject `IApprovalService`, call `PreApproveAsync("web-search")` after agent creation
6. **`Taim.Connectors/ClaudeCode/ClaudeCodeConnector.cs`** — accept `IConfiguration`, optional `workingDirectory`
7. **`Taim.Connectors/ConnectorExtensions.cs`** — update `ClaudeCodeConnector` registration
8. **`src/backend/Taim.Api/Dockerfile`** — multi-stage rewrite (Node.js + claude CLI + MCP servers)
9. **`docker-compose.yml`** — build context, `BRAVE_API_KEY`, `Workspace__Root`, `workspaces-data` volume
10. **`.env.example`** — document `BRAVE_API_KEY`
11. **`Taim.Tests/DeveloperAgentTests.cs`** — smoke tests
12. **CLAUDE.md updates** — all modules touched

## Acceptance Criteria

- [ ] **AC-1**: When a goal that produces Developer or QA agents is submitted, those agents do NOT produce strategy reports — only executive agents (CEO/CTO/CMO/CFO/HR) generate reports on the Reports page. Developer/QA agents appear on the team graph with `status: idle` immediately.
- [ ] **AC-2**: `WorkerKickoffAsync` makes zero LLM calls — verified by confirming no `AgentLog` notifications arrive for Developer/QA agents saying "proposing KPIs" or "running kickoff strategy".
- [ ] **AC-3**: Every newly created agent has a pre-seeded `agent_and_tool` approval for `web-search` in the `approvals` table — verified by querying the DB after a goal is submitted: `SELECT count(*) FROM approvals WHERE tool_name='web-search' AND scope='agent_and_tool'` equals the number of agents created.
- [ ] **AC-4**: An executive agent (e.g., CEO) that has an open action with a web search can complete that action without any user approval interaction — the action transitions from `open` → `in_progress` → `done` autonomously.
- [ ] **AC-5**: Inside the running `taim-api` Docker container, `node --version` and `claude --version` both succeed — verified by `docker exec taim-api-1 node --version` and `docker exec taim-api-1 claude --version`.
- [ ] **AC-6**: Inside the running container, `mcp-servers/web-search/index.js` exists and `mcp-servers/web-search/node_modules` is populated — verified by `docker exec taim-api-1 ls mcp-servers/web-search/node_modules | head -3`.
- [ ] **AC-7**: When `BRAVE_API_KEY` is set in the environment, an executive agent with a web_search action executes the search and the result appears in the action's description. When the key is absent, the tool returns a graceful error string ("web search not configured") rather than crashing.
- [ ] **AC-8**: `ClaudeCodeConnector.RunClaudeCode` uses `/app/workspaces` as the default working directory when `workingDirectory` is not supplied — verified by checking the tool call in agent_chat_history.
- [ ] **AC-9**: When a Developer agent attempts `claude_code`, an `ApprovalRequest` appears in the Approvals page with clear description ("Alex (Developer) wants to call claude_code"). After the user approves with `scope=AgentAndTool`, a subsequent `POST /api/actions/{id}/execute` completes the action without hitting the approval gate again.
- [ ] **AC-10 (CLAUDE.md)**: `Taim.Agents/CLAUDE.md`, `Taim.Connectors/CLAUDE.md`, `infra/CLAUDE.md`, root `CLAUDE.md`, and `docs/user-guide.md` are all updated before marking spec `done`.

## Test Plan

**Smoke tests** (add to `Taim.Tests/DeveloperAgentTests.cs`):

- [ ] `GET /api/reports?taskId=` returns zero reports whose `agentName` matches a Developer or QA agent (after a goal is submitted and kickoff completes)
- [ ] `GET /api/approvals` returns at least one pre-seeded approval per agent after team assembly (scope `agent_and_tool`, toolName `web-search`, status `approved`)
- [ ] `docker exec taim-api-1 node --version` exits 0 (test infrastructure check)

**Manual self-build test** (key milestone):

- [ ] Submit goal: *"Write a brief README for this project describing what TAIM is"*
- [ ] Bootstrap creates a small team; exec agents do strategy kickoff; at least one agent gets a documentation-writing action
- [ ] Action executes; agent calls `web_search` autonomously (no approval needed); eventually calls `complete_task`
- [ ] Action transitions to `done`; `docs/` or workspace contains an output file (if Developer agent was created and approved)

## CLAUDE.md Updates Required

- [ ] `Taim.Agents/CLAUDE.md` — add `WorkerAgentBase`, `WorkerKickoffAsync`, `IsWorkerRole` to the Agent Architecture section; update `AgentOrchestrator` description
- [ ] `Taim.Connectors/CLAUDE.md` — update ClaudeCode section: requires `claude` CLI in Docker; workspace root config; update status from `(planned)` to `(Sprint 4)`
- [ ] `infra/CLAUDE.md` — add `workspaces-data` volume; document `BRAVE_API_KEY` env var; note Dockerfile now requires repo root build context
- [ ] Root `CLAUDE.md` — update build state: `Developer agents with ClaudeCode connector` → `✅ Sprint 4`
- [ ] `docs/user-guide.md` — add section: "Developer Agents", "Approving Code Writes", "Workspaces"
- [ ] `METRICS.md` — update sprint progress and self-build gate (5/6)
- [ ] `PROCESS.md` Section 7 — update Sprint 4 status to ✅ Done

## Review

**Date:** 2026-05-18
**Result:** PASS
**Notes:** All 10 ACs implemented. Worker kickoff (AC-1/AC-2): IsWorkerRole routes Developer/QA to WorkerKickoffAsync with zero LLM calls. Pre-seeded approvals (AC-3): AgentFactory.CreateAsync calls PreApproveAsync for every agent. Docker infra (AC-5/AC-6): Dockerfile installs Node.js + @anthropic-ai/claude-code + MCP server deps (alpine-based runtime, node v24 via apk). Graceful fallback (AC-7): WebSearchConnector returns error string when BRAVE_API_KEY absent. Workspace config (AC-8): ClaudeCodeConnector reads Workspace:Root from IConfiguration. Approval gate (AC-9): existing ApprovalEndpoints handle claude_code approvals.
