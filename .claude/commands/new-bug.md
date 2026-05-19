Report a bug. Usage: /new-bug "symptom description"

You are acting as the QA agent. Your job is to document a bug clearly enough that any future agent can reproduce and fix it.

## Step 1: Investigate

Before writing the bug report:
1. Reproduce the bug (if possible from the description)
2. Check `docker compose logs taim-api --tail=50` for backend errors
3. Check browser console for frontend errors
4. Read the relevant module CLAUDE.md to understand the expected behavior

## Step 2: Determine Severity

| Severity | Apply when |
|---|---|
| **Critical** | System down, data loss, security vulnerability, authentication broken |
| **High** | Core feature completely broken, no workaround available |
| **Medium** | Feature degraded, workaround exists |
| **Low** | Cosmetic, minor UX issue, warning in logs |

## Step 3: Create the Bug Report

File: `specs/bugs/BUG-NNN-short-description.md`

Numbering: check the highest existing BUG number in `specs/bugs/` and increment.

Use `specs/_bug-template.md` as the base.

Required fields:
- **Symptom**: What you observe (not what you think causes it)
- **Reproduction steps**: Exact steps starting from a clean state
- **Expected vs actual**: What should happen vs what does happen
- **Severity**: with justification

## Step 4: Triage

Based on severity, update `METRICS.md` Known Issues table:
```
| BUG-NNN | severity | summary | sprint N |
```

Critical bugs: interrupt the current sprint — update `PROCESS.md` Section 7 (current sprint) to add the bug fix as P0.

High bugs: add to next sprint backlog.

Medium/Low: add to backlog only.

## Step 5: (If You Can Fix It Now)

If the fix is clear and small, fix it in the same session:
1. Fix the code
2. Fill in `## Root Cause` and `## Fix` in the bug spec
3. Add a regression test named `BUG_NNN_ShortDescription`
4. Set spec status → `verified`
5. Update METRICS.md Known Issues (remove the entry or mark resolved)

If the fix is complex, stop at Step 4 and let the next sprint handle it.
