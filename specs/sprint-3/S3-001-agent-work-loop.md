---
id: S3-001
title: Agent Work Loop
sprint: 3
status: done
created: 2026-05-18
updated: 2026-05-18
---

# S3-001 — Agent Work Loop

## Problem Statement

After kickoff, every agent goes `Idle` with a set of open `Actions` in the database. Nothing executes them. TAIM produces strategy plans but cannot carry them out — agents never do any real work. Sprint 3 closes this gap: each agent automatically picks up its assigned open actions, runs an LLM tool-use loop to execute them, and reports results. This is the core loop that separates TAIM from a planning tool.

## Solution Overview

A new `ActionWorker` class runs a multi-turn LLM loop for a single action. The loop builds a context from the action's title/description and the agent's charter, calls the LLM with an appropriate tool set, processes tool calls, and terminates when the LLM invokes a synthetic `complete_task` tool. Approval gating is preserved: tool calls without a long-lived approval block the action and create an `ApprovalRequest` in the existing approval queue.

After `KickoffTeamAsync` completes, `AgentOrchestrator` fires background work loops for all agents that have open actions. The loop is also re-triggerable via a new API endpoint so users can un-block stalled actions after approving a tool.

Tool assignment is codified in a static `ConnectorMapping` helper: executive roles get `web_search`; Developer/QA roles additionally get `claude_code`.

## Data Model

### No new tables

The existing `actions` table already supports all required status transitions. Two new status strings are used — `in_progress` and `blocked` — alongside the existing `open` and `done`. No DDL change is required; `status` is an unconstrained `VARCHAR`.

`agent_chat_history` (Sprint 0, `Taim.Memory`) is reused for turn storage. Session key convention for action work loops: `action:{actionId}`.

## Core Interface

### `Taim.Agents/Shared/IActionExecutor.cs` (new)

```csharp
namespace Taim.Agents.Shared;

/// <summary>
/// Resolves agent + chat client + tools for an action and fires the work loop
/// in a background Task. Registered as Scoped in DI.
/// </summary>
public interface IActionExecutor
{
    /// <summary>
    /// Triggers execution of the given action. Returns false if the action is not
    /// in a triggerable state (anything other than 'open' or 'blocked').
    /// The actual loop runs in a background Task; this method returns immediately.
    /// </summary>
    Task<bool> TriggerAsync(Guid tenantId, Guid actionId, CancellationToken ct = default);
}
```

### `Taim.Agents/Shared/ActionWorker.cs` (new)

`ActionWorker` is a plain class — **not in DI**. Instantiated by `IActionExecutor` and `AgentOrchestrator` with resolved dependencies.

```
ActionWorker(
    IActionService actionService,
    IApprovalService approvalService,
    INotificationService notifications,
    IChatHistoryProvider chatHistory,
    ILogger logger
)

ExecuteAsync(tenantId, taskId, action, agent, chatClient, tools, ct)
  → transitions action through open → in_progress → done | blocked
  → stores each turn in agent_chat_history
  → calls complete_task synthetic tool when done
```

### `Taim.Agents/Shared/ConnectorMapping.cs` (new)

```csharp
public static class ConnectorMapping
{
    public static IReadOnlyList<string> GetConnectorIds(AgentRole role) => role switch
    {
        AgentRole.Developer
        or AgentRole.QaEngineer
        or AgentRole.QaManager => ["web_search", "claude_code"],
        _ => ["web_search"]
    };
}
```

## Loop Algorithm

```
ExecuteAsync(tenantId, taskId, action, agent, chatClient, tools, ct):

1. UpdateAction(action.Id, status="in_progress")
   Push action_updated notification

2. Create completion signal: TaskCompletionSource<(string status, string summary)>
   Create completeTaskTool = AIFunctionFactory.Create(
       (string status, string summary) => { completionSignal.SetResult(...); return "ok"; },
       name: "complete_task",
       description: "Call when done or blocked. status='done'|'blocked'. summary=outcome."
   )
   allTools = tools + [completeTaskTool]

3. Load history = chatHistory.GetHistoryAsync(agent.Id, sessionKey="action:{action.Id}")
   if history is empty:
     add ChatMessage(System, BuildWorkSystemPrompt(agent, action))
     add ChatMessage(User, "Execute this action: {action.Title}\n\n{action.Description}")

4. Loop (max 15 turns):
   a. response = chatClient.GetResponseAsync(messages, new ChatOptions { Tools = allTools }, ct)
   b. Store response message in history + agent_chat_history table
   c. If completionSignal completed → break
   d. If response has no tool calls → treat as implicit done (status="done", summary=response.Text)
   e. For each tool call in response:
      i.   If tool == complete_task → handled in (c)
      ii.  existing = approvalService.CheckLongLivedAsync(agent.Id, toolName, args, ct)
           If existing is null:
             approvalService.CreateAsync(...)
             UpdateAction(status="blocked")
             Push action_updated notification (metadata: blockedReason="awaiting approval for {toolName}")
             Push agent_status_changed notification (status=WaitingApproval)
             UpdateAgentStatus(WaitingApproval)
             return  ← stop loop
           If existing.Approved == false:
             Add tool result message: "Tool {toolName} was denied by user"
             continue loop
      iii. Execute tool call → result
           Add tool result as ChatMessage(Tool, result)
           Store in agent_chat_history
   f. If no tool calls and no complete_task → implied done (see d)

5. On loop exit:
   (finalStatus, summary) = completionSignal result OR ("done", last response text)
   UpdateAction(status=finalStatus, description=action.Description + "\n\nResult: " + summary)
   Push action_updated notification
   UpdateAgentStatus(Idle)
   Push agent_status_changed notification

6. On unhandled exception:
   UpdateAction(status="blocked", description appended with "\n\nError: " + ex.Message)
   Push action_updated notification
   UpdateAgentStatus(Idle)
   log error
```

### Work System Prompt Template

```
You are {agent.Name}, {agent.Role}.
Charter: {agent.Charter}

You have been assigned the following task:
"{action.Title}"
{action.Description if non-empty}

Use the tools available to you to complete this task.
When you are finished — or if you cannot proceed — call complete_task with:
  status: "done" if you completed the task
  status: "blocked" if you cannot proceed (explain why in summary)
  summary: a brief description of what was done or why you are blocked

Be concrete. Prefer action over deliberation. Do not ask clarifying questions — make reasonable assumptions.
```

## API Contract

### `POST /api/actions/{id}/execute`

Triggers (or re-triggers) execution of a specific action. Intended for:
- Re-triggering a `blocked` action after the user approves a tool in the Approvals UI
- Manually starting a specific action (e.g., for testing)

- **Auth:** Bearer required
- **Path param:** `id` (UUID of the action)
- **Request body:** none
- **Response 202:** `{ "message": "Execution started" }`
- **Response 404:** action not found
- **Response 409:** `{ "error": "Action is not in a triggerable state", "status": "done" }` — if action is already `done` or `in_progress`

### `GET /api/actions?taskId=` (existing — no change)

Status `in_progress` and `blocked` are already valid return values via the existing `ActionRecord.Status` string field.

## UI/UX

No new UI pages. The existing Actions panel in TeamView already has the correct color scheme defined in the User Guide:
- **blue** border = `open`
- **amber** border = `in_progress`
- **red** border = `blocked`
- **green** border = `done`

Verify that `TeamView.tsx` maps `in_progress` → amber and `blocked` → red (these are already in `types.ts` as `ActionStatus`). If the color map in `TeamView.tsx` is missing these mappings, add them.

```
Actions Panel (TeamView sidebar) — in-progress state:
┌──────────────────────────────────────────────┐
│ ACTIONS (4)                                  │
│ ▐ (blue)   Research competitors   [unassigned]│
│ ▐ (amber)  Define tech stack      [Alex CTO] │  ← in_progress
│ ▐ (green)  Draft budget forecast  [Sam CFO]  │  ← done
│ ▐ (red)    Hire engineering lead  [HR]       │  ← blocked
└──────────────────────────────────────────────┘
```

Activity feed will show `ACTION` entries for each status transition and agent log entries for tool invocations.

## Implementation Order

Work through these files in order; build after each one.

1. **`Taim.Agents/Shared/ConnectorMapping.cs`** — static role → connector ID mapping (no deps)
2. **`Taim.Agents/Shared/ActionWorker.cs`** — the loop (depends on Core interfaces + MEAI)
3. **`Taim.Agents/Shared/ActionExecutor.cs`** — Scoped service implementing `IActionExecutor`
4. **`Taim.Agents/Shared/IActionExecutor.cs`** — interface (alongside ActionExecutor.cs)
5. **`AgentOrchestrator.cs`** — add `ExecuteAssignedActionsAsync`; call it at end of `KickoffTeamAsync` per agent
6. **`Taim.Api/Endpoints/ActionEndpoints.cs`** — add `POST /api/actions/{id}/execute`
7. **`Taim.Api/Program.cs`** — no change needed (MapActionEndpoints already registered)
8. **Register `IActionExecutor`** in `Taim.Agents/AgentExtensions.cs` (or wherever agents are registered in DI) — `services.AddScoped<IActionExecutor, ActionExecutor>()`
9. **`src/frontend/taim-web/src/features/team-view/TeamView.tsx`** — verify/fix action status color map
10. **`Taim.Tests/ActionWorkerTests.cs`** — smoke tests (live stack)
11. **CLAUDE.md updates** — all modules touched

## DI Registration

```csharp
// Wherever Taim.Agents registers its services (check existing AgentExtensions.cs or Program.cs)
services.AddScoped<IActionExecutor, ActionExecutor>();
// ActionWorker is NOT registered — instantiated directly within scopes
```

`ActionExecutor` depends on:
- `IServiceScopeFactory` (to create a scope for the background task)
- `IActionService` (to look up the action + agent)
- `IAgentRegistry` (to look up agent definition)
- `IProviderFactory` (to create IChatClient for the agent)
- `IBudgetService` (to wrap client with budget middleware)
- `IConnectorRegistry` (to resolve tools)
- `ILogger<ActionExecutor>`

## Acceptance Criteria

- [ ] **AC-1**: After `KickoffTeamAsync` completes, any agent that has at least one `open` action assigned to it automatically begins executing those actions in a background task — verified by watching the activity feed show `ACTION` events within ~10 seconds of kickoff completing.
- [ ] **AC-2**: During execution, the action's `status` field changes from `open` → `in_progress`, and an `action_updated` SignalR notification is pushed to the frontend before any tool calls are made.
- [ ] **AC-3**: The LLM receives a tool list appropriate for its role: executive agents get `web_search`; Developer/QA agents get `web_search` + `claude_code`.
- [ ] **AC-4**: When the LLM calls `complete_task` with `status="done"`, the action status transitions to `done` and an `action_updated` notification is pushed.
- [ ] **AC-5**: When the LLM calls `complete_task` with `status="blocked"`, the action status transitions to `blocked`, an `action_updated` notification is pushed, and the summary is appended to the action's description.
- [ ] **AC-6**: If the loop runs for 15 turns without a `complete_task` call, the action is set to `blocked` with description noting "max turns reached".
- [ ] **AC-7**: If a tool call has no long-lived approval: an `ApprovalRequest` is created via `IApprovalService.CreateAsync`, the action status is set to `blocked`, the agent status is set to `WaitingApproval`, and an `agent_status_changed` notification is pushed.
- [ ] **AC-8**: `POST /api/actions/{id}/execute` returns 202 and triggers the work loop for an action in `open` or `blocked` state; returns 409 for `done` or `in_progress`.
- [ ] **AC-9**: Each LLM turn's messages (assistant + tool results) are stored in `agent_chat_history` with session key `action:{actionId}`, so a subsequent `/execute` call resumes from the prior conversation.
- [ ] **AC-10**: An unhandled exception in the work loop sets the action to `blocked` (not `done`), appends the error to the action description, and resets the agent status to `Idle`.
- [ ] **AC-11**: `TeamView.tsx` correctly renders `in_progress` as amber border and `blocked` as red border in the Actions panel — verified visually in the browser.
- [ ] **AC-12 (CLAUDE.md)**: `Taim.Agents/CLAUDE.md`, `Taim.Api/CLAUDE.md`, `Taim.Connectors/CLAUDE.md`, root `CLAUDE.md`, and `docs/user-guide.md` are all updated before marking the spec `done`.

## Test Plan

**Smoke tests** (add to `Taim.Tests/ActionWorkerTests.cs`):

- [ ] `POST /api/actions/{id}/execute` returns 202 for an open action
- [ ] `POST /api/actions/{id}/execute` returns 409 for a done action
- [ ] `POST /api/actions/{id}/execute` returns 404 for a non-existent action
- [ ] After calling execute, polling `GET /api/actions?taskId=` eventually returns the action with status != `open` (within 60 seconds — the LLM is live)

**Manual verification**:

- [ ] Submit a goal → wait for kickoff to complete → watch activity feed for `ACTION` events showing tool calls
- [ ] Confirm at least one action transitions to `done` or `blocked` within 2 minutes
- [ ] Check that action status colors update in real-time in the TeamView sidebar

**E2E addition** (update `src/ui-tests/tests/user-journey.spec.ts`):

- [ ] After team assembles and kickoff completes, wait up to 3 minutes; assert at least one action has status `done` or `blocked` (not `open`)

## CLAUDE.md Updates Required

- [ ] `Taim.Agents/CLAUDE.md` — add `ActionWorker`, `ActionExecutor`, `ConnectorMapping` sections; update `AgentOrchestrator` description to include `ExecuteAssignedActionsAsync`
- [ ] `Taim.Api/CLAUDE.md` — add `POST /api/actions/{id}/execute` to Endpoint Groups table
- [ ] `Taim.Connectors/CLAUDE.md` — update "Tool Assignment by Role" status from `(planned)` to `(Sprint 3, via ConnectorMapping in Taim.Agents)`
- [ ] Root `CLAUDE.md` build state table — update `Agent work loop` row from `🔲 Sprint 3` to `✅ Sprint 3`
- [ ] `docs/user-guide.md` — add section explaining that agents now automatically execute Actions after kickoff, what the status colors mean, and how to use the Approvals UI to unblock a blocked action
- [ ] `METRICS.md` — update sprint progress, self-build gate (4/6), test counts
- [ ] `PROCESS.md` Section 7 — update Sprint 3 status to ✅ Done, add Sprint 4 notes

## Review

**Date:** —
**Result:** —
**Notes:** —
