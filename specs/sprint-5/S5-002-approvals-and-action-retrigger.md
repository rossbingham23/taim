---
id: S5-002
title: Approval History and Action Re-trigger
sprint: 5
status: done
created: 2026-05-18
updated: 2026-05-19
---

# S5-002 — Approval History and Action Re-trigger

## Problem Statement

Three separate usability gaps in the current actions/approvals workflow:

1. **Approvals page shows UUIDs instead of agent names.** `Approvals.tsx` line 11 sets `agentName: a.agentId` — a bug. The agent ID is displayed in place of the name.

2. **No approval audit trail.** `GET /api/approvals` only returns `pending` approvals. Once a decision is made, it disappears. There is no way to review past approve/deny decisions, which makes debugging agent behavior impossible.

3. **No way to manually trigger a stuck action.** If an action sits `open` or `blocked` (e.g. the meeting-generated actions from a previous test run), there is no UI affordance to re-trigger it. The `POST /api/actions/{id}/execute` endpoint exists but is unreachable from the UI.

## Solution Overview

**Backend:** Add `GET /api/approvals/history?taskId=` endpoint that returns decided (non-pending) approvals for a task, ordered by `decided_at` descending. No schema changes needed — the `decided_at` column already exists.

**Frontend — Approvals fix:** Call `listAgents()` alongside `getApprovals()` and resolve each approval's `agentId` to a name before rendering.

**Frontend — Approval history tab:** Add a "History" tab to the Approvals page showing decided approvals with agent name, tool, decision, scope, and timestamp.

**Frontend — Action re-trigger:** Add a "Run" button (▶) to each row in TeamView's Actions panel for `open` and `blocked` actions. Clicking it calls `POST /api/actions/{id}/execute` via a new `executeAction(id)` function in `api.ts`.

## Data Model

No schema changes. The `approvals` table already has `decided_at TIMESTAMPTZ` and `status TEXT` (values: `pending`, `approved`, `denied`).

## API Contract

### `GET /api/approvals/history?taskId=`
- Auth: Bearer required
- Query: `taskId` (required UUID)
- Returns all approvals for the task where `status != 'pending'`, ordered by `decided_at DESC`
- Response 200: `ApprovalResponse[]` (same shape as existing `ApprovalResponse`)
- Response 400: `{ "error": "taskId query parameter is required" }`

**Backend implementation:** Add `GetHistoryAsync(Guid tenantId, Guid taskId, CancellationToken ct)` to `IApprovalService` and implement in `ApprovalService`. Query: `WHERE tenant_id = @t AND task_id = @taskId AND status != 'pending' ORDER BY decided_at DESC`.

Note: the `approvals` table has a `task_id` column. Confirm this before implementing — if absent, fall back to joining through agents (get agent IDs for the task, then filter approvals by those agent IDs).

## UI/UX

### Approvals Page — Agent Name Fix + History Tab

```
┌─────────────────────────────────────────────────────────┐
│ Approvals                                               │
│                                                         │
│  [Pending (2)]  [History (14)]                          │
│  ─────────────────────────────────────                  │
│                                                         │
│  History tab:                                           │
│  ┌─────────────────────────────────────────────────┐   │
│  │  Alex (CTO)  web_search  ✓ Approved  agent+tool │   │
│  │  2026-05-18 14:32                               │   │
│  ├─────────────────────────────────────────────────┤   │
│  │  Sam (Dev)   claude_code  ✗ Denied   once       │   │
│  │  2026-05-18 14:28                               │   │
│  └─────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

### TeamView Actions Panel — Run Button

```
┌──────────── ACTIONS (3) ────────────┐
│ ▐ Define tech stack  [CTO]  open  ▶ │  ← ▶ calls POST /api/actions/{id}/execute
│ ▐ Draft go-to-market [CMO]  done    │  ← done: no button
│ ▐ Hire engineering   [HR ]  blocked ▶│  ← blocked: show button
└─────────────────────────────────────┘
```

The ▶ button is only shown for `open` and `blocked` actions. On click: disable the button, call `executeAction(id)`, re-enable on response (success or error). No full-page reload — the SignalR `action_updated` event will update the status automatically.

## Acceptance Criteria

- [x] **AC-1**: The Approvals page displays the agent's name (e.g. "Alex") instead of the agent UUID.
- [x] **AC-2**: `GET /api/approvals/history?taskId=` endpoint exists and returns decided approvals (approved + denied), ordered by `decided_at DESC`. Returns 400 if `taskId` is missing.
- [x] **AC-3**: The Approvals page has a "History" tab that shows decided approvals with: agent name, tool name, decision (Approved/Denied), scope, and `decided_at` timestamp.
- [x] **AC-4**: `executeAction(actionId: string)` function exists in `api.ts` calling `POST /api/actions/{id}/execute`.
- [x] **AC-5**: TeamView Actions panel shows a ▶ "Run" button for each action with status `open` or `blocked`.
- [x] **AC-6**: Clicking the ▶ button calls `executeAction` and disables the button during the request. The button re-enables after the request completes. No full-page navigation occurs.
- [x] **AC-7**: Actions with status `done`, `in_progress`, or `cancelled` do not show the ▶ button.

## Implementation Order

1. `src/backend/Taim.Core/Approvals/ApprovalModels.cs` — add `GetHistoryAsync` to `IApprovalService`
2. `src/backend/Taim.Data/Services/ApprovalService.cs` — implement `GetHistoryAsync`
3. `src/backend/Taim.Api/Endpoints/ApprovalEndpoints.cs` — add `GET /history` route
4. `src/frontend/taim-web/src/lib/api.ts` — add `executeAction(id)` function; add `listApprovalHistory(taskId)` function
5. `src/frontend/taim-web/src/features/approvals/Approvals.tsx` — fix agentName, add History tab
6. `src/frontend/taim-web/src/features/team-view/TeamView.tsx` — add ▶ Run button to action rows

## Test Plan

**Smoke test** (add to `Taim.Tests/ApprovalTests.cs` or a new `Sprint5Tests.cs`):
- [x] `GET /api/approvals/history?taskId=<valid>` returns 200 with array (may be empty for a fresh task)
- [x] `GET /api/approvals/history` (no taskId) returns 400

**Manual verification** (after rebuild):
- Approvals page: confirm agent names appear instead of UUIDs
- History tab: visible and shows decided approvals after approving/denying at least one
- TeamView Actions: confirm ▶ button appears for `open` and `blocked` actions
- Click ▶ on an open action: confirm action transitions to `in_progress` via SignalR update

## CLAUDE.md Updates Required

- [x] `src/backend/Taim.Core/CLAUDE.md` — add `GetHistoryAsync` to `IApprovalService` description
- [x] `src/backend/Taim.Api/CLAUDE.md` — add `GET /api/approvals/history` to endpoints table
- [x] Root `CLAUDE.md` — Sprint 5 row: change 🔲 to ✅ when complete

## Review

**Date:** 2026-05-19
**Result:** —
**Notes:** —
