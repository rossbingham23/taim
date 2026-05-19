---
id: S5-001
title: KPI Dashboard Page
sprint: 5
status: done
created: 2026-05-18
updated: 2026-05-18
---

# S5-001 — KPI Dashboard Page

## Problem Statement

The `KpiDashboard` component was built in Sprint 0 and renders a KPI tree correctly, but there is no route to reach it. Users have no way to see the KPIs that agents proposed during kickoff. The backend already supports `GET /api/kpis?taskId=` which returns a full hierarchy, but the frontend's `KpiResponse` type is missing the `children` field, so the tree can't be rendered. This sprint adds the missing page, fixes the type, and adds a navigation link from the task view.

## Solution Overview

Three small changes, all frontend:

1. **Fix `KpiResponse` type** — add `children?: KpiResponse[]` to the interface in `api.ts`. The backend's `GetTaskHierarchyRootsAsync` already returns nested children; the frontend type just doesn't model them.

2. **Create `KpiPage.tsx`** — a new feature page at `src/frontend/taim-web/src/features/kpi/KpiPage.tsx`. Reads `:taskId` from the URL, calls `listRootKpis(taskId)`, passes the result (cast to `KpiNode[]`) to `KpiDashboard`. Shows a loading state and an empty state message.

3. **Add route and navigation** — add `/tasks/:taskId/kpis` route in `App.tsx`. Add a "KPIs" link in the TeamView tab bar (alongside the existing "Activity" / "Actions" / "Meetings" tabs in the sidebar area).

No backend changes are required.

## Data Model

No changes.

## API Contract

No new endpoints. Existing:

### `GET /api/kpis?taskId=`
- Already exists in `KpiEndpoints.cs`
- Returns `KpiNode[]` from `kpiService.GetTaskHierarchyRootsAsync` — already hierarchical with `children`
- The frontend type `KpiResponse` is missing `children?: KpiResponse[]` — this is the only fix needed

## UI/UX

### KPI Page (`/tasks/:taskId/kpis`)

```
┌────────────────────────────────────────────────────────────┐
│ ← Back to Goal                                             │
│                                                            │
│  KPIs for: [Task Goal Text]                                │
│                                                            │
│  ┌─ CEO (Alex) ───────────────────────────────────────┐   │
│  │  Revenue Growth                     Target: 20%    │   │
│  │  Customer Acquisition               Target: 500    │   │
│  │                                                    │   │
│  │  ┌─ CTO (Sam) ─────────────────────────────────┐  │   │
│  │  │  Code Coverage                  Target: 80%  │  │   │
│  │  │  Deploy Frequency               Target: 5/wk │  │   │
│  │  └─────────────────────────────────────────────┘  │   │
│  └────────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────────┘
```

### Navigation Link in TeamView

Add a "KPIs" button to the existing navigation row in TeamView (the row that has the task title and status). Link to `/tasks/:taskId/kpis`.

```
TeamView header row:
  [← Goals]   Goal: "Build a platform..."   [active]   [KPIs ↗]
```

## Acceptance Criteria

- [x] **AC-1**: `KpiResponse` in `api.ts` has `children?: KpiResponse[]` field.
- [x] **AC-2**: Navigating to `/tasks/:taskId/kpis` renders `KpiPage` without a 404.
- [x] **AC-3**: `KpiPage` calls `listRootKpis(taskId)` and passes results to `KpiDashboard`. KPI names and hierarchy are visible when KPIs exist.
- [x] **AC-4**: `KpiPage` shows a loading indicator while the request is in flight.
- [x] **AC-5**: `KpiPage` shows "No KPIs available" when the array is empty.
- [x] **AC-6**: TeamView has a "KPIs" link that navigates to `/tasks/:taskId/kpis`.
- [x] **AC-7**: `App.tsx` has the `/tasks/:taskId/kpis` route pointing to `KpiPage`.

## Implementation Order

1. `src/frontend/taim-web/src/lib/api.ts` — add `children?: KpiResponse[]` to `KpiResponse` interface
2. `src/frontend/taim-web/src/features/kpi/KpiPage.tsx` — create the page component
3. `src/frontend/taim-web/src/App.tsx` — add the route
4. `src/frontend/taim-web/src/features/team-view/TeamView.tsx` — add the "KPIs" navigation link

## Test Plan

**Smoke test** — no new HTTP endpoints; no smoke test needed.

**Manual verification** (after `docker compose build taim-web && docker compose up -d taim-web`):
- Navigate to a task that has completed kickoff
- Click the "KPIs" link → confirm `/tasks/:id/kpis` loads
- Confirm KPI names appear in the tree
- Navigate to a fresh task with no KPIs → confirm "No KPIs available" message

## CLAUDE.md Updates Required

- [x] `src/frontend/taim-web/CLAUDE.md` — add `features/kpi/KpiPage.tsx` to the structure, add `/tasks/:taskId/kpis` to routes
- [x] Root `CLAUDE.md` — Sprint 5 row: change 🔲 to ✅ when complete

## Review

**Date:** —
**Result:** —
**Notes:** —
