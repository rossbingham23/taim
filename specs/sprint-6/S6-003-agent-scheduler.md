---
id: S6-003
title: Agent Scheduler
sprint: 6
status: done
created: 2026-05-19
updated: 2026-05-19
---

# S6-003 — Agent Scheduler

## Problem Statement

Agent work loops currently only start when an action is first created (auto-trigger in `AgentOrchestrator.KickoffTeamAsync`) or when the user manually clicks the Run button (S5-002). There is no background sweep to pick up:
- Actions created after the initial trigger window (e.g. by meeting action items after the auto-trigger already ran)
- `blocked` actions that should be retried after the system resumes
- Actions assigned to agents that were idle at trigger time but later became available

Without a scheduler, human intervention (the Run button) is required for every orphaned action. The platform cannot be self-running.

**Prerequisites (hard gates):** S6-001 (task termination + `ITaskCancellationRegistry`) and S6-002 (system stop + `ISystemStopService`) MUST be complete and deployed before this spec is implemented. The scheduler must not be turned on without those safety gates.

## Solution Overview

A .NET `BackgroundService` (`AgentScheduler`) registered in `Taim.Api`. Every N seconds (default 30, configurable):

1. Check `ISystemStopService.IsStoppedAsync()` → if stopped, skip this tick entirely.
2. Load all tasks with `status = 'active'` for **all tenants**.
3. For each task, load all actions with `status IN ('open', 'blocked')`.
4. For each triggerable action: load its assigned agent; skip if agent is not `idle`.
5. Call `IActionExecutor.TriggerAsync(tenantId, actionId)` — `TriggerAsync` is already idempotent (guards against non-triggerable statuses).

No new data model is required. The existing `actions.status`, `agents.status`, and `tasks.status` fields provide the safety gate.

## Data Model

No changes. The existing schema is sufficient:
- `tasks.status = 'active'` — scheduler-safe tasks
- `agents.status = 'idle'` — scheduler-safe agents
- `actions.status IN ('open', 'blocked')` — triggerable actions
- The `scheduled_tasks` table (already in schema) is NOT used for this feature — it's reserved for future per-agent recurring task scheduling, which is out of scope for this sprint.

## Core Service Changes

### `Taim.Api/Background/AgentScheduler.cs` — new file

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Taim.Agents.Shared;
using Taim.Core.Actions;
using Taim.Core.Agents;
using Taim.Core.System;
using Taim.Core.Teams;

namespace Taim.Api.Background;

public sealed class AgentScheduler(
    IServiceScopeFactory scopeFactory,
    IOptions<SchedulerOptions> options,
    ILogger<AgentScheduler> logger) : BackgroundService
{
    private static readonly HashSet<string> TriggerableStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "open", "blocked" };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.IntervalSeconds);
        logger.LogInformation("AgentScheduler started; interval={Interval}s", options.Value.IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(interval, stoppingToken);
            await RunTickAsync(stoppingToken);
        }
    }

    private async Task RunTickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        try
        {
            var systemStop = sp.GetRequiredService<ISystemStopService>();
            if (await systemStop.IsStoppedAsync(ct))
            {
                logger.LogDebug("AgentScheduler tick skipped — system stop active");
                return;
            }

            var taskService    = sp.GetRequiredService<ITaskService>();
            var actionService  = sp.GetRequiredService<IActionService>();
            var agentRegistry  = sp.GetRequiredService<IAgentRegistry>();
            var executor       = sp.GetRequiredService<IActionExecutor>();

            // GetAllAsync is tenant-scoped via RLS — need to query across all tenants.
            // Use a raw query or a new cross-tenant method. See implementation note below.
            var activeTasks = await taskService.GetSchedulerCandidatesAsync(ct);

            var triggered = 0;
            foreach (var task in activeTasks)
            {
                var actions = await actionService.GetForTaskAsync(task.TenantId, task.Id, ct);
                foreach (var action in actions)
                {
                    if (!TriggerableStatuses.Contains(action.Status)) continue;
                    if (!action.AgentId.HasValue) continue;

                    var agent = await agentRegistry.GetAsync(task.TenantId, action.AgentId.Value, ct);
                    if (agent is null || agent.Status != AgentStatus.Idle) continue;

                    var fired = await executor.TriggerAsync(task.TenantId, action.Id, ct);
                    if (fired) triggered++;
                }
            }

            if (triggered > 0)
                logger.LogInformation("AgentScheduler tick: triggered {Count} action(s)", triggered);
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (Exception ex)
        {
            logger.LogError(ex, "AgentScheduler tick failed");
        }
    }
}
```

### `SchedulerOptions`

```csharp
public sealed class SchedulerOptions
{
    public int IntervalSeconds { get; set; } = 30;
    public bool Enabled { get; set; } = true;
}
```

### Cross-Tenant Query — `ITaskService.GetSchedulerCandidatesAsync`

`ITaskService.GetAllAsync` is tenant-scoped (RLS). The scheduler needs **all** active tasks across all tenants. Add a new method:

```csharp
// In ITaskService (Taim.Core/Teams/TeamModels.cs):
Task<IReadOnlyList<TaskRecord>> GetSchedulerCandidatesAsync(CancellationToken ct = default);
```

Implementation in `TaskService.cs` uses **raw SQL with RLS bypassed** via the service account (the app DB user is not restricted to a single tenant — RLS only applies via the `app.tenant_id` session variable set by `RlsInterceptor`). A raw `ExecuteSqlRawAsync` or `FromSqlRaw` that selects `WHERE status = 'active'` across all rows works because the scheduler executes outside of any HTTP request scope (no RLS interceptor sets `app.tenant_id` in background service scopes).

```csharp
public async Task<IReadOnlyList<TaskRecord>> GetSchedulerCandidatesAsync(CancellationToken ct = default)
{
    // Background service scope — RLS interceptor does NOT run here (no HTTP request).
    // The DB user can read all rows; no SET app.tenant_id needed.
    return await _db.Tasks
        .Where(t => t.Status == "active")
        .Select(t => new TaskRecord(t.Id, t.TenantId, t.Goal, t.Status, t.BudgetId, t.CreatedAt, t.UpdatedAt))
        .ToListAsync(ct);
}
```

## `Program.cs` Changes

```csharp
// Configuration binding
builder.Services.Configure<SchedulerOptions>(
    builder.Configuration.GetSection("Scheduler"));

// Conditional registration
var schedulerOptions = builder.Configuration
    .GetSection("Scheduler").Get<SchedulerOptions>() ?? new();
if (schedulerOptions.Enabled)
    builder.Services.AddHostedService<AgentScheduler>();
```

## `appsettings.json` / `docker-compose.yml`

Add to `appsettings.json`:
```json
"Scheduler": {
  "IntervalSeconds": 30,
  "Enabled": true
}
```

Add to `docker-compose.yml` under `taim-api` environment:
```yaml
- Scheduler__Enabled=${SCHEDULER_ENABLED:-true}
- Scheduler__IntervalSeconds=${SCHEDULER_INTERVAL_SECONDS:-30}
```

## API Contract

No new HTTP endpoints. The scheduler is entirely internal.

## UI/UX

No new UI. The scheduler fires silently in the background. The existing SignalR `action_updated` notifications from `ActionExecutor` will surface activity in the frontend as usual.

## Safety Gate Summary

The scheduler ONLY fires `TriggerAsync` when ALL of the following are true:
1. `ISystemStopService.IsStoppedAsync()` returns `false` (S6-002)
2. `task.Status == "active"` — terminated tasks are excluded (S6-001)
3. `agent.Status == AgentStatus.Idle` — busy agents are not double-triggered
4. `action.Status IN ('open', 'blocked')` — `TriggerAsync` also guards this internally

No action will be triggered for terminated tasks, terminated agents, or when the system stop flag is set.

## Acceptance Criteria

- [ ] **AC-1**: `AgentScheduler` is a `BackgroundService` in `Taim.Api/Background/`. It starts on application startup when `Scheduler:Enabled = true`.
- [ ] **AC-2**: Each tick, the scheduler checks `ISystemStopService.IsStoppedAsync()`. If stopped, the tick is skipped entirely (no DB queries, no triggers).
- [ ] **AC-3**: The scheduler only fires `TriggerAsync` for actions where: `task.status = 'active'`, `agent.status = 'idle'`, `action.status IN ('open', 'blocked')`.
- [ ] **AC-4**: The scheduler never fires `TriggerAsync` for tasks with `status = 'terminated'`.
- [ ] **AC-5**: `ITaskService.GetSchedulerCandidatesAsync` exists and returns all `active` tasks across all tenants (cross-tenant read, no RLS filter).
- [ ] **AC-6**: Interval is configurable via `Scheduler:IntervalSeconds` (default 30). `Scheduler:Enabled = false` disables the service entirely without a code change.
- [ ] **AC-7**: Scheduler can be disabled via environment variable `Scheduler__Enabled=false` in docker-compose.
- [ ] **AC-8**: Scheduler tick failures (DB error, etc.) are logged but do not crash the service or affect subsequent ticks.
- [ ] **AC-9**: Setting `Scheduler:Enabled = false` in the test environment prevents spurious test interference.

## Implementation Order

1. `Taim.Core/Teams/TeamModels.cs` — add `GetSchedulerCandidatesAsync` to `ITaskService`
2. `Taim.Data/Services/TaskService.cs` — implement `GetSchedulerCandidatesAsync`
3. `Taim.Api/Background/AgentScheduler.cs` — new `BackgroundService`
4. `Taim.Api/Background/SchedulerOptions.cs` — new options class
5. `Taim.Api/Program.cs` — bind options, conditionally register `AddHostedService<AgentScheduler>`
6. `src/backend/appsettings.json` — add `Scheduler` section
7. `docker-compose.yml` — add `Scheduler__Enabled` and `Scheduler__IntervalSeconds` env vars

## Test Plan

**Smoke test** (add to `Taim.Tests/Sprint6Tests.cs`):
- [ ] `GET /api/system/status` returns `{ "stopped": false }` before scheduler tick (prerequisite: system not stopped)
- [ ] After creating an active task with an open action assigned to an idle agent, wait 35 seconds and verify the action transitions to `in_progress` (scheduler triggered it)

**Unit-style verification:**
- Disable scheduler in tests via `Scheduler:Enabled=false` in test config to prevent background interference with other tests.

## CLAUDE.md Updates Required

- [ ] `Taim.Core/CLAUDE.md` — add `GetSchedulerCandidatesAsync` to `ITaskService` description
- [ ] `Taim.Api/CLAUDE.md` — add `AgentScheduler` background service note; add `Scheduler:IntervalSeconds` config
- [ ] Root `CLAUDE.md` — update build state for Sprint 6 when complete

## Review

**Date:** —
**Result:** —
**Notes:** —
