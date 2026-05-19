Show the current sprint status and what to build next.

Read the product plan at `/home/rossb/.claude/plans/orleans-why-oreans-humble-blossom.md` for the full roadmap.

## Current State (Sprint 1 complete as of 2026-05-18)

### Done
- Actions table + `IActionService` + `GET/POST/PATCH /api/actions`
- Delegation dispatch in `AgentOrchestrator.KickoffAgentAsync`
- Actions list in TeamView sidebar

### Sprint 2 — Meetings
Files to create/modify:
- `Taim.Core/Meetings/IMeetingOrchestrator.cs` — meeting runner interface
- `Taim.Agents/Meetings/MeetingOrchestrator.cs` — LLM-driven turn-based meeting
- `Taim.Api/Endpoints/MeetingEndpoints.cs` — POST + GET meetings
- `TeamView.tsx` — meetings section below activity
- Trigger `kickoff_sync` meeting after `KickoffTeamAsync` completes

### Sprint 3 — Agent Work Loop
- `AgentOrchestrator.ExecuteActionAsync` — agent receives action, runs tool loop, completes/blocks
- Tool invocation with approval gates via `IApprovalService`

### Sprint 4 — Developer Tools
- Wire `ClaudeCodeConnector` to Developer/QA agents
- `ConnectorRegistry.GetToolsForRole(role)` → tool list

### Sprint 5+
- KPI dashboard page (component exists, needs route)
- Approval audit trail
- Sub-team spawning
- Scheduling
