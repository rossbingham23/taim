---
id: BUG-002
title: ActionWorker crashes on every execution — TakeLast not translatable by EF Core
severity: critical
sprint-found: 5
status: fixed
---

# BUG-002 — ActionWorker crashes on every execution — TakeLast not translatable by EF Core

## Symptom

Every triggered action immediately goes `blocked` with no meaningful description. API logs show:

```
fail: Taim.Agents.Shared.ActionWorker[0]
      ActionWorker unhandled error for action <id>
      System.InvalidOperationException: The LINQ expression '...TakeLast(@p)' could not be translated.
```

## Reproduction Steps

1. Submit a goal and wait for kickoff
2. Any agent whose action is triggered will immediately block
3. All work loops are dead — no LLM calls are ever made

## Root Cause

`ChatHistoryProvider.LoadAsync` used `.TakeLast(maxMessages)` on an EF Core `IQueryable`. EF Core cannot translate `TakeLast` into SQL. Throws `InvalidOperationException` at query execution time.

## Fix

`src/backend/Taim.Memory/Episodic/ChatHistoryProvider.cs` — replaced `TakeLast` with `OrderByDescending + Take + re-sort`:

```csharp
.OrderByDescending(h => h.Sequence)
.Take(maxMessages)
.OrderBy(h => h.Sequence)
```

This is semantically equivalent (latest N messages in chronological order) and translates cleanly to SQL.

**Files changed:** `Taim.Memory/Episodic/ChatHistoryProvider.cs`
