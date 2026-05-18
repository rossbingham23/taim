---
id: BL-003
title: KPI Dashboard Page
sprint: 5
status: draft
created: 2026-05-18
updated: 2026-05-18
---

# BL-003 — KPI Dashboard Page

## Problem Statement

KPIs are proposed and saved during agent kickoff. The `KpiDashboard` component exists in the component library with Storybook stories. But there is no page route that displays it. The user has no way to see what KPIs the agents set for themselves or how they're performing.

## Solution Overview

Add a `/tasks/:taskId/kpis` route wired to the existing `KpiDashboard` component. Fetch KPI data from `GET /api/kpis?taskId=`. Add a link from the TeamView header. Add a "Record Value" button for manual KPI updates.

## Implementation

1. Add route in `App.tsx`: `<Route path="/tasks/:taskId/kpis" element={<KpiPage />} />`
2. Create `src/features/kpis/KpiPage.tsx` — fetches KPIs, passes to `KpiDashboard`
3. Add nav link in `TeamView.tsx` header: "KPIs →"
4. Add `POST /api/kpis/:id/values` manual value entry button in `KpiDashboard`

No backend changes needed — the API already exists.

## Dependencies

None. Can be done in any sprint as it's purely frontend.

## Spec Status: Draft
