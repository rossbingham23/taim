---
id: S6-001
title: Task Termination
sprint: 6
status: done
created: 2026-05-19
updated: 2026-05-19
---

# S6-001 — Task Termination

## Problem Statement

There is no way to stop a running task. The DB has thousands of agents and open actions from test runs. Once a task is started, its agents and work loops run until they complete naturally. There is no mechanism to: (a) cancel all pending work for a task, (b) interrupt work loops already in flight, or (c) mark a task as "done, ignore all its work items." Without this, adding a scheduler in S6-003 would immediately wake up thousands of old agents simultaneously. Task termination is a hard prerequisite for the scheduler.

## Solution Overview

Four changes:

1. **`ITaskCancellationRegistry`** — a singleton that holds a `CancellationTokenSource` per task. `ActionExecutor` registers before launching a loop and uses the task's CTS token (linked with `CancellationToken.None`) instead of the HTTP request token. `MeetingOrchestrator` similarly uses the task's CTS token. This allows external cancellation of any running loop by task ID.

2. **`ITaskService.TerminateAsync`** — cascades termination via bulk SQL: task→`terminated`, agents→`terminated`, open/in_progress actions→`cancelled`, in_progress meetings→`failed`. Then signals `ITaskCancellationRegistry.Cancel(taskId)` to abort running loops.

3. **`POST /api/tasks/{id}/terminate`** — HTTP endpoint. Returns 204 on success, 404 if task not found, 409 if already terminated.

4. **Frontend** — "Terminate" button in TeamView header (red, beside "KPIs" link). Visible when task status is `active` or `bootstrapping`. Pushes `TaskTerminated` SignalR notification to update the UI.

## Data Model

### SQL — `infra/postgres/init.sql`

Update the tasks table status comment only (no structural change):
```sql
-- Before:
status TEXT NOT NULL DEFAULT 'pending',
    -- 'pending' | 'bootstrapping' | 'running' | 'paused' | 'completed' | 'failed'

-- After:
status TEXT NOT NULL DEFAULT 'pending',
    -- 'pending' | 'bootstrapping' | 'active' | 'terminated' | 'failed'
```

No ALTER TABLE needed — `status` is TEXT and already accepts arbitrary values.

All cascade targets already exist:
- `agents.status` already allows `'terminated'` (DB comment confirms)
- `actions.status` already allows `'cancelled'`
- `meetings.status` already allows `'failed'`

### New `NotificationKind`

Add to `Taim.Core/Notifications/NotificationKind.cs`:
```csharp
TaskTerminated,   // task + all agents/actions cancelled
```

Also register in the SignalR serialization (no code change needed — `JsonStringEnumConverter` handles it automatically via `snake_case_lower`).

## Core Interface Changes

### `Taim.Core/Teams/TeamModels.cs` — add to `ITaskService`

```csharp
Task TerminateAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);
```

### `Taim.Core/Agents/ITaskCancellationRegistry.cs` — new file

```csharp
namespace Taim.Core.Agents;

public interface ITaskCancellationRegistry
{
    CancellationToken Register(Guid taskId);   // idempotent; creates CTS if none exists
    void Cancel(Guid taskId);                  // signals the task's CTS
    void Unregister(Guid taskId);              // cleanup after task is done/terminated
}
```

### `Taim.Agents/Shared/TaskCancellationRegistry.cs` — new file

```csharp
using System.Collections.Concurrent;
using Taim.Core.Agents;

namespace Taim.Agents.Shared;

public sealed class TaskCancellationRegistry : ITaskCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _sources = new();

    public CancellationToken Register(Guid taskId)
        => _sources.GetOrAdd(taskId, _ => new CancellationTokenSource()).Token;

    public void Cancel(Guid taskId)
    {
        if (_sources.TryGetValue(taskId, out var cts))
            cts.Cancel();
    }

    public void Unregister(Guid taskId)
    {
        if (_sources.TryRemove(taskId, out var cts))
            cts.Dispose();
    }
}
```

Registered as **singleton** in DI:
```csharp
services.AddSingleton<ITaskCancellationRegistry, TaskCancellationRegistry>();
```

## `TaskService.TerminateAsync` Implementation

`Taim.Data/Services/TaskService.cs` — new method:

```csharp
public async Task TerminateAsync(Guid tenantId, Guid taskId, CancellationToken ct = default)
{
    // Bulk SQL — single transaction is fine; RLS already applied via interceptor
    await _db.Database.ExecuteSqlRawAsync(@"
        UPDATE tasks   SET status = 'terminated', updated_at = now()
        WHERE id = {0} AND tenant_id = {1};

        UPDATE agents  SET status = 'terminated', updated_at = now()
        WHERE task_id = {0} AND tenant_id = {1};

        UPDATE actions SET status = 'cancelled',  updated_at = now()
        WHERE task_id = {0} AND tenant_id = {1}
          AND status IN ('open', 'in_progress');

        UPDATE meetings SET status = 'failed'
        WHERE task_id = {0} AND tenant_id = {1}
          AND status = 'in_progress';
    ", taskId, tenantId, ct);
}
```

Note: signal `ITaskCancellationRegistry.Cancel(taskId)` is done in the **endpoint handler** after calling `TerminateAsync`, not inside the service (services don't depend on Taim.Agents).

## `ActionExecutor` Changes

`Taim.Agents/Shared/ActionExecutor.cs` — inject `ITaskCancellationRegistry` (constructor param), then link tokens:

```csharp
// Replace:
_ = Task.Run(async () => { ... await worker.ExecuteAsync(..., ct); });

// With (inside Task.Run, after freshAction is loaded):
var taskToken = _taskCancellationRegistry.Register(freshAction.TaskId);
// Use taskToken for the worker (the original ct is the HTTP request token, already completed)
await worker.ExecuteAsync(tenantId, freshAction.TaskId, freshAction, agent, chatClient, tools, taskToken);
```

## `ActionWorker` Changes

`Taim.Agents/Shared/ActionWorker.cs` — handle cancellation gracefully:

```csharp
public async Task ExecuteAsync(...)
{
    try
    {
        await RunLoopAsync(...);
    }
    catch (OperationCanceledException)
    {
        // Task was terminated or system was stopped — cascade cleanup already done by endpoint
        logger.LogInformation("ActionWorker loop cancelled for action {ActionId}", action.Id);
    }
    catch (Exception ex)
    {
        // existing error handling unchanged
    }
}
```

## `AgentOrchestrator` Changes

`Taim.Agents/Shared/AgentOrchestrator.cs` — inject `ITaskCancellationRegistry` and pass task token when firing meeting:

```csharp
// When starting the kickoff_sync meeting in background:
var taskToken = _taskCancellationRegistry.Register(taskId);
_ = Task.Run(async () => { ... await meetingOrchestrator.RunAsync(request, chatClients, taskToken); });
```

## API Contract

### `POST /api/tasks/{taskId}/terminate`
- Auth: Bearer required
- Path: `taskId` (UUID)
- Response 204: terminated successfully
- Response 404: task not found (or not owned by tenant)
- Response 409: `{ "error": "Task is already terminated" }`

**Handler logic:**
1. Resolve `tenantId` from JWT
2. `taskService.GetAsync(tenantId, taskId)` → 404 if null
3. If `task.Status == "terminated"` → 409
4. `taskService.TerminateAsync(tenantId, taskId, ct)`
5. `taskCancellationRegistry.Cancel(taskId)`
6. Push `NotificationKind.TaskTerminated` notification with `taskId` in metadata
7. Return 204

## UI/UX

### Terminate Button in TeamView

```
TeamView header:
  [← Goals]   Goal: "Build a platform..."   [active]   [KPIs ↗]   [Terminate ✕]
```

- Button only rendered when `task.status === 'active' || task.status === 'bootstrapping'`
- On click: confirmation dialog ("Terminate this task? All running agents and actions will be stopped and cancelled.")
- On confirm: call `POST /api/tasks/{id}/terminate`, disable button during request
- On success: frontend navigates back to Goals list (or shows `terminated` status inline)

### Goals List — Terminated Badge

The Goals page (TaskIntake) already shows a status badge per task. Add `terminated` → grey badge (same color as `cancelled` actions: `#475569`).

## Acceptance Criteria

- [ ] **AC-1**: `ITaskCancellationRegistry` interface exists in `Taim.Core/Agents/`. `TaskCancellationRegistry` implementation exists in `Taim.Agents/Shared/` and is registered as singleton.
- [ ] **AC-2**: `ActionExecutor` uses `ITaskCancellationRegistry.Register(taskId)` to get the task's CTS token and passes it to `ActionWorker.ExecuteAsync` instead of the HTTP request token.
- [ ] **AC-3**: `ActionWorker.ExecuteAsync` catches `OperationCanceledException` and exits cleanly (logs, no `blocked` status write).
- [ ] **AC-4**: `ITaskService` has `TerminateAsync`. `TaskService.TerminateAsync` bulk-updates task→`terminated`, agents→`terminated`, open/in_progress actions→`cancelled`, in_progress meetings→`failed`.
- [ ] **AC-5**: `POST /api/tasks/{id}/terminate` returns 204 on success, 404 if not found, 409 if already terminated.
- [ ] **AC-6**: After termination, `ITaskCancellationRegistry.Cancel(taskId)` is called — any in-flight loops exit via `OperationCanceledException`.
- [ ] **AC-7**: `NotificationKind.TaskTerminated` exists and is pushed by the endpoint after successful termination.
- [ ] **AC-8**: `AgentOrchestrator` uses the task's CTS token when launching the kickoff_sync meeting.
- [ ] **AC-9**: TeamView shows a "Terminate" button for active/bootstrapping tasks. Clicking it shows a confirmation dialog, calls the endpoint, and navigates back to Goals on success.
- [ ] **AC-10**: Goals page renders a `terminated` status badge.

## Implementation Order

1. `Taim.Core/Agents/ITaskCancellationRegistry.cs` — new interface
2. `Taim.Core/Notifications/NotificationKind.cs` — add `TaskTerminated`
3. `Taim.Core/Teams/TeamModels.cs` — add `TerminateAsync` to `ITaskService`
4. `Taim.Agents/Shared/TaskCancellationRegistry.cs` — new implementation
5. `Taim.Data/Services/TaskService.cs` — implement `TerminateAsync`
6. `Taim.Agents/Shared/ActionExecutor.cs` — inject registry, use task CTS token
7. `Taim.Agents/Shared/ActionWorker.cs` — handle `OperationCanceledException`
8. `Taim.Agents/Shared/AgentOrchestrator.cs` — use task CTS token for meeting
9. `Taim.Api/Endpoints/TaskEndpoints.cs` — add terminate route
10. `Program.cs` — register `TaskCancellationRegistry` as singleton
11. `infra/postgres/init.sql` — update status comment
12. Frontend: `api.ts` `terminateTask(taskId)`, TeamView button, Goals badge

## Test Plan

**Smoke test** (add to `Taim.Tests/`):
- [ ] `POST /api/tasks/{id}/terminate` on a valid active task returns 204
- [ ] Second `POST /api/tasks/{id}/terminate` on same task returns 409
- [ ] `GET /api/tasks/{id}` after termination shows `status = 'terminated'`
- [ ] `GET /api/actions?taskId={id}` after termination shows all actions `cancelled`

## CLAUDE.md Updates Required

- [ ] `Taim.Core/CLAUDE.md` — add `ITaskCancellationRegistry` to interfaces table; add `TerminateAsync` to `ITaskService`; add `TaskTerminated` to `NotificationKind` enum list
- [ ] `Taim.Agents/CLAUDE.md` — add `TaskCancellationRegistry`; update `ActionExecutor` and `ActionWorker` descriptions; update `AgentOrchestrator` description
- [ ] `Taim.Api/CLAUDE.md` — add `POST /api/tasks/{id}/terminate` to endpoint table
- [ ] Root `CLAUDE.md` — add `ITaskCancellationRegistry` note to Critical Invariants; update build state table

## Review

**Date:** —
**Result:** —
**Notes:** —
