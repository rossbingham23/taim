---
id: BL-004
title: Sub-Team Spawning
sprint: 6
status: draft
created: 2026-05-18
updated: 2026-05-18
---

# BL-004 — Sub-Team Spawning

## Problem Statement

The CTO needs Developers. The CMO needs Marketing Specialists. Currently agents can only be created by the Bootstrap process at the start of a task. There is no mechanism for an executive agent to say "I need staff" and spawn a sub-team. Without this, the team is fixed at kickoff size and cannot grow organically to match the work.

## Solution Overview

A `spawn_team` tool call (available to executive agents during the work loop). The agent provides a list of `{ role, charter }` specs. The system creates agent records, runs kickoff for each new agent (with the executive as parent), wires them as direct reports. Requires user approval before spawning (cost gate).

## Key Design

```
Agent executes action, decides it needs staff
→ Calls spawn_team({ agents: [{ role: "developer", charter: "Build the auth module" }] })
→ ApprovalService gates on the user
→ On approval: AgentFactory.CreateFromSpecAsync (new agent in DB)
→ AgentOrchestrator.KickoffAgentAsync for each new agent
→ New agents appear in TeamView (team_update notification)
→ CEO can then dispatch actions to them
```

**Constraint:** Maximum sub-team size = 5 agents per spawn call. Maximum total agents per task = 20. Both enforced before creating agents.

## Dependencies

- Agent work loop (BL-001) — spawn_team is a tool call in the work loop
- `ProposeSubTeamAsync` already exists in `ExecutiveAgentBase` — may be reusable

## Spec Status: Draft
