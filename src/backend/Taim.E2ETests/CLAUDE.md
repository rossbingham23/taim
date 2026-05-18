# Taim.E2ETests — Playwright End-to-End Tests

Browser-level user journey tests using Playwright. Runs against the full live stack.

## Running

```bash
cd src/ui-tests
npm install
npx playwright install chromium
npx playwright test
```

Requires full Docker stack at `http://localhost:3000`. Run `./start.sh` first.

## Test Files

| File | Covers |
|---|---|
| `../ui-tests/tests/user-journey.spec.ts` | Full user journey: login → submit goal → team assembly → activity → console nav → goals list |

## Key Design Decisions

- **No API knowledge**: Tests only interact via the browser — no `fetch` calls, no direct DB access
- **Selectors are broad**: Uses regex selectors (`/Team \(\d+\)/`, `/activated|proposing|KPI/i`) to avoid fragile exact-string matching and to be resilient to LLM-generated agent names
- **Long timeouts**: Team assembly takes up to 3 minutes; the test waits up to 180s for agents to appear
- **`first()` on ambiguous selectors**: Goals list may show multiple tasks from prior runs; always use `.first()` when the same text can appear multiple times

## Timeout Configuration

`playwright.config.ts` sets `timeout: 300_000` (5 minutes per test) to accommodate LLM latency.

## Adding a New E2E Test

Add a new `*.spec.ts` in `src/ui-tests/tests/`. Follow the user-journey spec as a pattern:
- Use `page.goto`, `page.fill`, `page.click` for interactions
- Use `expect(page.locator(...)).toBeVisible({ timeout: N })` for assertions
- Use broad selectors — the UI text is often LLM-generated
