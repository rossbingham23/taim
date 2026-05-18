# TAIM Frontend

React 19, Vite, TypeScript. Single-page app served by nginx which proxies `/api/` and `/hubs/` to the backend.

## Structure

```
src/
  main.tsx              — app entry point, router setup
  App.tsx               — layout + route definitions
  lib/
    api.ts              — all HTTP calls (relative URLs, Bearer auth header)
    signalr.ts          — SignalR connection + onNotification() subscription helper
    types.ts            — shared TypeScript interfaces (Agent, Task, Notification, etc.)
    auth.tsx            — useAuth() hook, ProtectedRoute component
  features/
    auth/Login.tsx      — login page
    task-intake/        — goal submission form
    team-view/TeamView.tsx   — task detail: team graph + agent cards + sidebar
    reports/Reports.tsx      — executive reports (fetches + streams via SignalR)
    approvals/          — approval queue
    settings/           — provider config UI
  components/
    TeamGraph/          — SVG org chart from nodes + edges
    AgentCard/          — agent status card
    ExecutiveReport/    — report display card
    BudgetMeter/        — budget spend visualization
    KpiDashboard/       — KPI tree display
    ApprovalQueue/      — approval request cards
    MeetingViewer/      — meeting transcript
```

## API Calls

All calls go through `lib/api.ts`:
- Base URL is `''` (empty string — same origin)
- Nginx in the Docker container proxies `/api/` → `http://taim-api:8080`
- Auth header: `Authorization: Bearer <token>` from `localStorage.getItem('taim_token')`
- 401 responses auto-redirect to `/login` and clear the token

## SignalR

```ts
// Connect (called after login)
await connectSignalR(token)

// Subscribe to all notifications
const unsubscribe = onNotification(notification => {
  if (notification.kind === 'executive_report') { ... }
})

// Cleanup
return unsubscribe  // in useEffect return
```

`Notification` shape (matches backend `Notification` record with snake_case `kind`):
```ts
interface Notification {
  id: string
  kind: NotificationKind   // 'executive_report' | 'team_update' | ...
  tenantId: string
  title: string
  body: string
  metadata: Record<string, unknown>
  createdAt: string
}
```

## Role & Status Enum Values

Roles are **camelCase** strings (matching HTTP JSON): `'ceo'`, `'cto'`, `'productManager'`, `'qaEngineer'`, etc.

Notification kinds are **snake_case** strings (matching SignalR JSON): `'executive_report'`, `'team_update'`, `'agent_status_changed'`, etc.

## Build & Dev

```bash
npm run dev       # Vite dev server on port 5173 (proxies /api/ to localhost:5000)
npm run build     # production build → dist/
npm run storybook # component stories on port 6006
```

In Docker: `npm run build` then nginx serves `dist/` on port 3000.

## Key Components

**TeamGraph** — pure SVG layout. `computeLayout()` builds a top-down hierarchy tree. Nodes are colored by `AgentStatus`, labeled with role abbreviation + agent name. Click triggers `onNodeClick` callback.

**TeamView** — fetches task + agent list on mount; re-fetches on `team_update`/`agent_status_changed` SignalR events; polls every 3s while status is `'bootstrapping'`.

**Reports** — accepts `?taskId=` query param; fetches existing reports via `GET /api/reports`; appends new reports on `executive_report` SignalR events; deduplicates by `id`.
