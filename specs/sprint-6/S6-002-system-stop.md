---
id: S6-002
title: System-Wide Emergency Stop
sprint: 6
status: done
created: 2026-05-19
updated: 2026-05-19
---

# S6-002 — System-Wide Emergency Stop

## Problem Statement

Once S6-003 (scheduler) is live, agents will execute autonomously and continuously. There must be a single circuit-breaker that halts all agent activity instantly — not just for one task, but for the entire platform. This is needed during incidents, debugging, or when the platform behaves unexpectedly. The system stop must be: (a) activatable from the UI in one click, (b) immediately effective for all running and queued work, (c) non-destructive (no data lost; resume resumes from where things were).

**Prerequisite:** S6-001 must be complete before this spec is implemented (the `ITaskCancellationRegistry` pattern is assumed to exist).

## Solution Overview

A Redis flag (`taim:system:stop`) acts as the circuit breaker:
- When set: `ActionWorker` detects it at the top of each loop turn and exits cleanly (action left as `in_progress`, agent left as `active` — state is preserved for resume)
- When cleared: the scheduler (S6-003) will naturally pick up where things left off on the next tick

Three parts:
1. **`ISystemStopService`** — Scoped service backed by Redis. Reads and writes the flag.
2. **`POST /api/system/stop` + `POST /api/system/resume` + `GET /api/system/status`** — Three endpoints.
3. **Settings UI** — "System Controls" section with current status and a toggle button.

## Data Model

No DB changes. Redis only.

**Redis key:** `taim:system:stop`
- Exists and set to `"1"` → system stopped
- Does not exist (or deleted) → system running

## Core Interface

### `Taim.Core/System/ISystemStopService.cs` — new file

```csharp
namespace Taim.Core.System;

public interface ISystemStopService
{
    Task<bool> IsStoppedAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task ResumeAsync(CancellationToken ct = default);
}
```

### `Taim.Data/Services/SystemStopService.cs` — new file

```csharp
using StackExchange.Redis;
using Taim.Core.System;

namespace Taim.Data.Services;

public sealed class SystemStopService(IConnectionMultiplexer redis) : ISystemStopService
{
    private const string Key = "taim:system:stop";
    private IDatabase Db => redis.GetDatabase();

    public async Task<bool> IsStoppedAsync(CancellationToken ct = default)
        => await Db.KeyExistsAsync(Key);

    public async Task StopAsync(CancellationToken ct = default)
        => await Db.StringSetAsync(Key, "1");

    public async Task ResumeAsync(CancellationToken ct = default)
        => await Db.KeyDeleteAsync(Key);
}
```

Registered as **Scoped**:
```csharp
services.AddScoped<ISystemStopService, SystemStopService>();
```

## `ActionWorker` Changes

`Taim.Agents/Shared/ActionWorker.cs` — inject `ISystemStopService` as constructor param; check at the top of each loop turn in `RunLoopAsync`:

```csharp
for (int turn = 0; turn < MaxTurns; turn++)
{
    // Emergency stop check (cheapest path — one Redis GET per turn)
    if (await _systemStopService.IsStoppedAsync(ct))
    {
        logger.LogInformation("System stop active — halting loop for action {ActionId}", action.Id);
        return;  // exit RunLoopAsync; action stays in_progress, agent stays active
    }

    // ... rest of turn (LLM call, tool execution, etc.)
}
```

`ActionExecutor` resolves `ISystemStopService` from its background scope and passes it to `ActionWorker`.

## API Contract

### `POST /api/system/stop`
- Auth: Bearer required
- Request body: empty
- Response 204: system stop flag set
- Behavior: calls `systemStopService.StopAsync()`; pushes `NotificationKind.SystemStopped` notification

### `POST /api/system/resume`
- Auth: Bearer required
- Request body: empty
- Response 204: system stop flag cleared
- Behavior: calls `systemStopService.ResumeAsync()`; pushes `NotificationKind.SystemResumed` notification

### `GET /api/system/status`
- Auth: Bearer required
- Response 200: `{ "stopped": true | false }`
- Used by the frontend to reflect current state on Settings page load

### New `NotificationKind` values

Add to `Taim.Core/Notifications/NotificationKind.cs`:
```csharp
SystemStopped,   // all agent activity halted
SystemResumed,   // agent activity resumed
```

## UI/UX

### Settings Page — System Controls Section

Add a new section at the top of `Settings.tsx`, above the provider fields:

```
┌──────────────────────────────────────────────────────┐
│ System Controls                                      │
│                                                      │
│  Agent Activity    ● Running                         │
│                    [Stop All Agents]                 │
│                                                      │
│  ┌──────────────────────────────────────────────┐   │
│  │ ℹ  Stopping halts all work loops immediately │   │
│  │    but preserves all task and action state.  │   │
│  │    Resume to continue from where you left    │   │
│  │    off.                                      │   │
│  └──────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────┘
```

When stopped, the button becomes "Resume All Agents" (green), status shows "● Stopped" (red).

**State management in `Settings.tsx`:**
- On mount: `GET /api/system/status` → set `isStopped` state
- Subscribe to SignalR `system_stopped` / `system_resumed` notifications → update state
- Button click: calls `stopSystem()` or `resumeSystem()` in `api.ts`

### New `api.ts` functions

```ts
export function getSystemStatus(): Promise<{ stopped: boolean }> {
  return request('/api/system/status')
}

export function stopSystem(): Promise<void> {
  return request('/api/system/stop', { method: 'POST' })
}

export function resumeSystem(): Promise<void> {
  return request('/api/system/resume', { method: 'POST' })
}
```

## Acceptance Criteria

- [ ] **AC-1**: `ISystemStopService` interface exists in `Taim.Core/System/`. `SystemStopService` implemented in `Taim.Data/Services/` backed by Redis key `taim:system:stop`.
- [ ] **AC-2**: `ActionWorker.RunLoopAsync` checks `ISystemStopService.IsStoppedAsync()` at the top of every turn. When stopped, the loop exits cleanly without changing action or agent status.
- [ ] **AC-3**: `POST /api/system/stop` sets the Redis flag and returns 204. `POST /api/system/resume` deletes the flag and returns 204. `GET /api/system/status` returns `{ "stopped": true|false }`.
- [ ] **AC-4**: `NotificationKind.SystemStopped` and `NotificationKind.SystemResumed` are pushed on stop/resume respectively.
- [ ] **AC-5**: Settings page has a "System Controls" section at the top. It shows the current stopped/running status.
- [ ] **AC-6**: Settings page "Stop All Agents" button calls `POST /api/system/stop`. When stopped, the button shows "Resume All Agents" and calls `POST /api/system/resume`.
- [ ] **AC-7**: Settings page fetches `GET /api/system/status` on mount to show current state without depending solely on SignalR.
- [ ] **AC-8**: Settings page updates its stopped/running display in real-time when `system_stopped` / `system_resumed` SignalR notifications arrive.

## Implementation Order

1. `Taim.Core/System/ISystemStopService.cs` — new interface
2. `Taim.Core/Notifications/NotificationKind.cs` — add `SystemStopped`, `SystemResumed`
3. `Taim.Data/Services/SystemStopService.cs` — new implementation
4. `Taim.Api/Endpoints/SystemEndpoints.cs` — new endpoint group
5. `Program.cs` — register `SystemStopService` as scoped, map `SystemEndpoints`
6. `Taim.Agents/Shared/ActionWorker.cs` — inject + check `ISystemStopService` per turn
7. `Taim.Agents/Shared/ActionExecutor.cs` — resolve `ISystemStopService` from scope, pass to worker
8. Frontend: `api.ts` new functions; `Settings.tsx` System Controls section

## Test Plan

**Smoke test** (add to `Taim.Tests/`):
- [ ] `GET /api/system/status` returns `{ "stopped": false }` initially
- [ ] `POST /api/system/stop` returns 204; subsequent `GET /api/system/status` returns `{ "stopped": true }`
- [ ] `POST /api/system/resume` returns 204; subsequent `GET /api/system/status` returns `{ "stopped": false }`

## CLAUDE.md Updates Required

- [ ] `Taim.Core/CLAUDE.md` — add `ISystemStopService` to interfaces table; add `SystemStopped`, `SystemResumed` to `NotificationKind` enum list
- [ ] `Taim.Data/CLAUDE.md` — add `SystemStopService` to services table
- [ ] `Taim.Api/CLAUDE.md` — add the three system endpoints to endpoint table
- [ ] `Taim.Agents/CLAUDE.md` — update `ActionWorker` description to mention system stop check
- [ ] `infra/CLAUDE.md` — note the Redis key `taim:system:stop`

## Review

**Date:** —
**Result:** —
**Notes:** —
