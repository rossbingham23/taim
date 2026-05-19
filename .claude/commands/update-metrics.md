Update METRICS.md at the end of a sprint. Usage: /update-metrics

## Step 1: Gather Current State

Run these to get accurate numbers:

```bash
# Build health
cd src/backend && dotnet build Taim.slnx 2>&1 | grep -E "^Build|error|Error" | tail -5

# Smoke test count
grep -r "\[Fact\]" src/backend/Taim.Tests/ | wc -l

# E2E test count
find src/ui-tests/tests -name "*.spec.ts" | wc -l

# Open bugs
ls specs/bugs/ 2>/dev/null | grep "^BUG" | wc -l

# Which sprint specs are done
grep -r "^status:" specs/ | grep -v template | sort
```

## Step 2: Update METRICS.md Sections

Update these sections:
1. **North Star Metric** — qualitative assessment: what can agents do today vs the target?
2. **Self-Build Readiness** — check/uncheck each gate item based on current state
3. **Sprint Progress** — update the status column for the completed sprint; set next sprint to "🔲 Ready"
4. **Feature Completion** — update status for any newly completed features
5. **Test Health** — update test counts and last-run date
6. **Build Health** — update last build date and status
7. **Known Issues** — add/remove bug entries
8. **Sprint Velocity** — add the completed sprint row

## Step 3: Update PROCESS.md Sprint Plan

In PROCESS.md Section 7 (Sprint Plan), update the sprint table:
- Mark completed sprint as ✅ Done with the correct "Verified" status
- Confirm the next sprint spec is in `specs/sprint-N/` and has status `ready`

## Step 4: Communicate to Stakeholder

After updating, write a brief sprint summary in the conversation:

```
Sprint N complete.
Shipped: [list features]
Tests: [count] smoke tests, [count] E2E tests — all passing
Known issues: [count critical], [count high]
Next sprint: [sprint goal]
```

This is the stakeholder update. Ross reads this to know what shipped.
