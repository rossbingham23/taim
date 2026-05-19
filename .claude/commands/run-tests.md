Run all tests and report results. Usage: /run-tests

Requires the full Docker stack running (./start.sh or docker compose up -d).

## Step 1: Build Check

```bash
cd src/backend && dotnet build Taim.slnx 2>&1 | tail -5
```
Expected: `0 Error(s)`. If errors, STOP and report them — do not proceed to tests.

## Step 2: Smoke Tests

```bash
cd src/backend && dotnet test Taim.Tests/Taim.Tests.csproj --logger "console;verbosity=normal" 2>&1 | tail -30
```

Report: passed/failed/total. If any fail, check the error message and determine if it's a new regression or a pre-existing issue.

## Step 3: E2E Tests

```bash
cd src/ui-tests && npx playwright test --reporter=list 2>&1 | tail -30
```

Expected: all tests pass. E2E tests are long (up to 5 minutes) — wait for them to complete.

If E2E fails: capture the error message. Check if it's a timing issue (increase timeout) vs a real regression.

## Step 4: TypeScript Check

```bash
cd src/frontend/taim-web && npm run build 2>&1 | tail -10
```

Expected: clean build, 0 errors.

## Step 5: Report

Write a results summary:

```
Test Run: YYYY-MM-DD HH:MM

BUILD: PASS | FAIL (N errors)
SMOKE TESTS: N/N passed
E2E TESTS: N/N passed
TYPESCRIPT: PASS | FAIL

Issues:
- [describe any failures]

Action needed:
- [create bug report for any new failures]
- [link to relevant spec if it's a regression]
```

If there are new failures, create bug reports using `/new-bug` for each one.

## Step 6: Update METRICS.md

Update the Test Health table with today's date and results.
