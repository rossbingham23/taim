---
id: BUG-NNN
title: Short description of the bug
severity: critical | high | medium | low
sprint-found: N
sprint-fix: N
status: open | in-progress | fixed | verified
reported: YYYY-MM-DD
fixed: YYYY-MM-DD
---

# BUG-NNN — Short Description

## Symptom

> What does the user or agent observe? What error message appears? What doesn't work?

## Reproduction Steps

1. Start the system with `./start.sh`
2. Log in as `admin@taim.local`
3. ...

## Expected Behavior

> What should happen?

## Actual Behavior

> What actually happens?

## Severity Justification

> Why is this severity level correct? What is the user/system impact?

## Environment

- Docker stack version: (describe any relevant infra state)
- Triggered by: (specific action, specific goal text, specific agent role)

---

## Root Cause

> Filled in after investigation.

The bug is caused by...

## Fix

**Files changed:**
- `path/to/file.cs` — description of change

**Why it fixes it:**
> Explain the fix in terms of the root cause.

## Regression Test

**Test added in:** `src/backend/Taim.Tests/XxxTests.cs`
**Test name:** `BUG_NNN_ShortDescription`

```csharp
[Fact]
public async Task BUG_NNN_ShortDescription()
{
    // Arrange: set up the condition that triggered the bug
    // Act: trigger the action
    // Assert: verify the bug does not occur
}
```

## Review

**Date:** —
**Result:** —
