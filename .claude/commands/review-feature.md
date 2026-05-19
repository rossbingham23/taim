Review a feature implementation against its spec. Usage: /review-feature specs/sprint-N/SN-NNN-feature-name.md

You are acting as the Reviewer for TAIM. Your job is to verify the implementation is correct, complete, and safe.

## Step 1: Load Context

1. Read the spec file (passed as argument)
2. Read CLAUDE.md for every module touched by this feature
3. Read `PROCESS.md` Section 3 (design invariants)

## Step 2: Verify Each Acceptance Criterion

For each AC in the spec, find the code that satisfies it. If you can't find it, it's a failure.

Mark each AC:
- ✅ Satisfied — code exists and implements the criterion correctly
- ❌ Missing — no code found
- ⚠️ Partial — code exists but doesn't fully satisfy the criterion

## Step 3: Invariant Check

Check every invariant from PROCESS.md Section 3:

- [ ] No `AddDbContextPool` anywhere in the new code
- [ ] Every new EF property has `.HasColumnName()`
- [ ] No `ChatResponseFormat.Json` passed to any LLM call
- [ ] Any background work that accesses DB uses `IServiceScopeFactory`
- [ ] No new executive agent classes registered in DI
- [ ] No manual `WHERE tenant_id = X` in EF queries (RLS handles it)
- [ ] LLM responses parsed with `AgentJson.Deserialize<T>()` (not `JsonSerializer.Deserialize`)
- [ ] New SignalR notification kinds are snake_case in frontend types
- [ ] New HTTP notification kinds are camelCase in frontend types

## Step 4: Security Check

- [ ] No string interpolation into SQL (all queries use EF or parameterized SQL)
- [ ] No secrets or API keys in code
- [ ] No XSS risk (React renders all user content as text, not HTML)
- [ ] No tenant isolation bypass (all queries go through RLS-filtered `TaimDbContext`)

## Step 5: Test Coverage

- [ ] At least one smoke test exists for the new API endpoint(s)
- [ ] The test calls the endpoint and checks the HTTP status code
- [ ] E2E test updated if feature is user-visible

## Step 6: Documentation

- [ ] Relevant CLAUDE.md files updated
- [ ] Root CLAUDE.md build state updated
- [ ] METRICS.md updated

## Step 7: Write Review Result

Append to the spec file:

```markdown
## Review
**Date:** YYYY-MM-DD
**Result:** PASS | FAIL
**Notes:**
- AC-1: ✅ satisfied
- AC-2: ❌ [describe what's missing]
- Invariants: all pass | [list failures]
- Security: pass | [list issues]
- Tests: pass | [what's missing]
```

Set spec `status: verified` (PASS) or back to `in-progress` (FAIL).

If FAIL, add TODO items directly in the spec's acceptance criteria section so the implementer knows exactly what to fix.
