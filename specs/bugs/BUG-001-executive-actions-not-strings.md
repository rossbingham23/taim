---
id: BUG-001
title: Executive kickoff fails — LLM returns actions as objects not strings
severity: high
sprint-found: 5
status: fixed
---

# BUG-001 — Executive kickoff fails — LLM returns actions as objects not strings

## Symptom

`AGENTLOG` event in activity feed:
```
Alex - Chief Executive Officer: kickoff failed — The JSON value could not be converted to System.String. Path: $.actions[0]
```

CEO (and potentially other executive agents) silently fail their kickoff. Their strategy report is not saved and their delegations are not dispatched. Other agents continue normally.

## Reproduction Steps

1. Submit any goal that produces a multi-executive team
2. Observe the activity feed for `kickoff failed` events
3. Check that the failing agent has no report and no actions were dispatched

## Expected vs Actual

**Expected:** `ExecutiveResponse.Actions` deserializes as `string[]` (plain task titles).

**Actual:** The LLM returns `actions` as an array of objects e.g. `[{"title": "…", "assignee": "…"}]`. `AgentJson.Deserialize<ExecutiveResponse>` fails because the C# type is `IReadOnlyList<string>`.

## Root Cause

`ExecutiveAgentBase.RunAsync` system prompt said "Respond ONLY with a JSON object matching the ExecutiveResponse schema" without defining the schema inline. The LLM inferred from context that `actions` should be rich objects.

## Fix

`src/backend/Taim.Agents/Executive/ExecutiveAgentBase.cs` — replaced the vague schema reference with an explicit JSON example in the system prompt, with a note clarifying that `actions` and `delegations` must be arrays of plain strings.

**Files changed:** `Taim.Agents/Executive/ExecutiveAgentBase.cs`
