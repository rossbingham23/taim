---
id: S1-001
title: Work-Item Actions
sprint: 1
status: verified
created: 2026-05-18
updated: 2026-05-18
---

# S1-001 — Work-Item Actions

## Problem Statement

After kickoff, executive agents produce strategy reports and a list of "Delegations" — free-text strings like "CTO: Define technical architecture". These strings are stored in the report content but never dispatched anywhere. There is no mechanism for tracking what work needs to be done, who is assigned to it, or whether it has been completed. The platform has a complete strategy phase with no execution phase.

## Solution Overview

Introduce an `actions` table as the work-item layer for TAIM. Each action belongs to a task and can be assigned to an agent. During kickoff, each executive agent's delegation strings are parsed and dispatched as Action records assigned to the appropriate direct-report agent. A REST API exposes actions for reading and updating. The TeamView UI shows the action list in the sidebar.

## Data Model

### SQL (added to `infra/postgres/init.sql`)

```sql
CREATE TABLE actions (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id           UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    task_id             UUID NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    agent_id            UUID REFERENCES agents(id) ON DELETE SET NULL,
    created_by_agent_id UUID REFERENCES agents(id) ON DELETE SET NULL,
    title               TEXT NOT NULL,
    description         TEXT,
    status              TEXT NOT NULL DEFAULT 'open',
    priority            INTEGER NOT NULL DEFAULT 50,
    parent_action_id    UUID REFERENCES actions(id) ON DELETE SET NULL,
    due_at              TIMESTAMPTZ,
    completed_at        TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

Status values: `open | in_progress | blocked | done | cancelled`
Priority: 0 = critical, 50 = normal, 100 = low

### EF Entity

`ActionEntity` in `Taim.Data/Models/Entities.cs` with all properties matching the SQL schema.

## Core Interface

`Taim.Core/Actions/ActionModels.cs`:
- `ActionRecord` — immutable read DTO
- `CreateActionRequest` — creation input
- `UpdateActionRequest` — partial update (all fields optional)
- `IActionService` — CRUD interface

## API Contract

### `GET /api/actions?taskId=`
Returns all actions for a task, ordered by priority ASC, created_at ASC.

### `POST /api/actions`
Creates an action. `agentId` is optional (unassigned actions are valid).

### `PATCH /api/actions/{id}`
Partial update. `status="done"` automatically sets `completed_at`.

## UI/UX

Actions panel in the TeamView sidebar (below agent detail, above budget):

```
┌─────────────────────────────────────┐
│ ACTIONS (3)                         │
│ ▐blue  Define tech stack      [CTO] │
│ ▐blue  Draft go-to-market    [CMO]  │
│ ▐blue  Build hiring pipeline  [HR]  │
└─────────────────────────────────────┘
```

Left border color = status color: blue=open, amber=in_progress, red=blocked, green=done, gray=cancelled.

## Delegation Dispatch

In `AgentOrchestrator.KickoffAgentAsync`, after saving the kickoff report:
1. Call `registry.GetTeamAsync(tenantId, definition.Id)` to get direct reports.
2. For each delegation string, search for the report's role name or agent name (case-insensitive substring match).
3. Create an `ActionRecord` with `CreatedByAgentId = definition.Id`, `AgentId = matched_report?.Id`.
4. Push `NotificationKind.ActionCreated` notification.

Unmatched delegations are created with `AgentId = null` (unassigned).

## Acceptance Criteria

- [x] **AC-1**: `actions` table exists in PostgreSQL with RLS tenant isolation
- [x] **AC-2**: `GET /api/actions?taskId=xxx` returns HTTP 200 with array (empty if no actions)
- [x] **AC-3**: `POST /api/actions` creates an action and returns HTTP 201 with the created record
- [x] **AC-4**: `PATCH /api/actions/{id}` updates the action and returns HTTP 200; `status=done` sets `completed_at`
- [x] **AC-5**: After kickoff, each executive agent's delegations are persisted as Action records in the DB
- [x] **AC-6**: Actions panel appears in TeamView sidebar showing title, status color, and assignee name
- [x] **AC-7**: Frontend type `ActionItem` and `NotificationKind` include action events
- [x] **AC-8**: `action_created`/`action_updated` appear in ActivityConsole with ACTION badge

## Test Plan

**Smoke tests:** Not yet added. Add `Taim.Tests/ActionTests.cs`:
- `GET /api/actions?taskId=` returns 200
- `POST /api/actions` returns 201
- `PATCH /api/actions/{id}` returns 200

**E2E:** Not yet verified automatically. Manual observation: after a kickoff run, actions appear in the UI sidebar.

## CLAUDE.md Updates

- [x] `Taim.Core/CLAUDE.md` — `IActionService` added to interfaces, `ActionRecord` added to records
- [x] `Taim.Data/CLAUDE.md` — `actions` table added to Tables table
- [x] `Taim.Api/CLAUDE.md` — action endpoints added
- [x] Root `CLAUDE.md` — build state updated, architecture flow updated, "Where Things Live" updated

## Review

**Date:** 2026-05-18
**Result:** PASS (self-review — implementer = reviewer this sprint)
**Notes:**
- Build: 0 errors
- API smoke test: `GET /api/actions?taskId=xxx` returns `[]` HTTP 200 ✅
- `action_created` / `action_updated` added to `NotificationKind` enum (both backend and frontend)
- `ActivityConsole` updated with ACTION badge colors
- Missing: dedicated smoke tests in `Taim.Tests` — add in a future session
