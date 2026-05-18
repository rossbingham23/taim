---
id: BL-001
title: Agent Work Loop
sprint: 3
status: draft
created: 2026-05-18
updated: 2026-05-18
---

# BL-001 — Agent Work Loop

## Problem Statement

Actions exist in the DB. Agents go Idle after kickoff. Nothing connects the two. An agent needs to be able to receive an assigned action, execute it (potentially using tools), and report the result. Without this loop, TAIM produces plans but does nothing with them.

## Solution Overview

`AgentOrchestrator.ExecuteActionAsync(agentId, actionId, tools)` — the core execution engine. An agent picks up an action, runs an LLM loop (up to N turns with tool calls), and transitions the action to `done` or `blocked`. Tool calls that require human approval gate on `IApprovalService`. The loop terminates when the LLM signals completion, exceeds a turn limit, or a non-recoverable error occurs.

## Key Design Decisions

**Turn loop:**
```
Action status → in_progress
Loop (max 15 turns):
  - Build context: action title/description, agent charter, KPIs, prior conversation
  - Call LLM with tools available
  - If LLM response has no tool calls → it's declaring done/blocked
  - If tool call needs approval → gate on IApprovalService, wait (or fail-open?)
  - Execute tool call → feed result back as next turn message
  - Store each turn in agent_chat_history
On exit:
  - status → done (with result) or blocked (with reason)
  - Push action_updated notification
  - Optionally trigger briefing meeting to manager
```

**Tool invocation:** The `tools` list is `IList<AITool>` from the connector registry. Passed to `ChatOptions`. The `Microsoft.Extensions.AI` abstraction handles tool dispatch.

**Approval gating:** Before executing a tool call, check `IApprovalService.CheckLongLivedAsync(agentId, toolName, params)`. If approval is required: push `ApprovalRequired` notification, pause the loop (store intermediate state), resume when approved. Pause mechanism: store the pending action + last LLM response in a checkpoint table or Redis.

## Dependencies

- Actions table (S1-001) ✅
- `IApprovalService` ✅
- `ChatHistoryProvider` (Taim.Memory) ✅
- Connector registry (Taim.Connectors) — needs `GetToolsForRole(AgentRole)` method

## Triggers for Loop

1. Post-meeting action items assigned to an agent
2. CEO manually dispatches an action to an agent
3. (Future) Scheduler fires a recurring action

## Spec Status: Draft

Full spec to be written at the start of Sprint 3. The above is sufficient to unblock Sprint 2 (no dependencies on the work loop for meetings).
