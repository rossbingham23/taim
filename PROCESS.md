# TAIM — Engineering Process

> **Every agent session working on TAIM must read this file after `CLAUDE.md`.**
> It defines how features are discovered, designed, built, reviewed, and shipped.

---

## 1. Governance & Roles

TAIM is built by Claude Code agent sessions. Each session plays one role:

| Role | Responsibility | Entry command |
|---|---|---|
| **Product Owner (PO)** | Writes specs, manages backlog, decides priorities | `/new-feature` |
| **Implementer** | Reads a spec, writes all code to satisfy acceptance criteria | `/implement-feature <spec-path>` |
| **Reviewer** | Reads the spec + diff, verifies correctness and invariants | `/review-feature <spec-path>` |
| **QA** | Writes and runs tests, reports failures as bugs | `/run-tests` |

One session can play multiple roles sequentially (e.g., implement → self-review → update docs).

The **user (Ross)** plays **Board Chair**: sets the vision, approves sprint plans, makes go/no-go calls on consequential decisions. The PO role is Claude's proxy for his product thinking.

---

## 2. Context Loading (Every Session)

Before writing any code or spec, load context in this order:

1. `CLAUDE.md` ← auto-loaded by Claude Code
2. `PROCESS.md` ← this file (read it)
3. `METRICS.md` ← current sprint state and open bugs
4. The spec for the feature being worked on (`specs/sprint-N/SN-NNN-name.md`)
5. CLAUDE.md for each **module being touched** (e.g., `Taim.Core/CLAUDE.md`, `Taim.Data/CLAUDE.md`)

This set of files is your complete working context. Do not start implementation without it.

---

## 3. Feature Lifecycle

### Stage 1 — Discovery (How We Know What to Build)

Sources, in priority order:
1. **Sprint roadmap** (Section 7 of this document) — the authoritative queue
2. **Bug reports** in `specs/bugs/` — critical/high bugs interrupt the current sprint
3. **Tech debt** found during implementation — captured as `specs/backlog/TD-NNN-*.md`
4. **User direction** — Ross identifies a new need; PO agent writes a spec

Every new feature or bug starts with a one-paragraph **problem statement** in a spec file before any code is written.

### Stage 2 — Prioritization (MoSCoW Per Sprint)

| Priority | Meaning | Rule |
|---|---|---|
| **P0 — Must** | Blocks self-build capability | Cannot ship sprint without it |
| **P1 — Should** | Core product loop, user value this sprint | Ship if time allows |
| **P2 — Could** | Nice to have | Only if P0+P1 are done |
| **P3 — Won't** | Not this sprint | Backlog entry only |

Sprint size: **3 features** maximum per sprint session. A sprint is a single focused Claude Code conversation (~1–3 hours of real time).

### Stage 3 — Design (Before Any Code)

Design is done in the spec file. A feature is **not ready to implement** until the spec has:
- [ ] Architecture: data model changes, new interfaces, service dependencies
- [ ] API contract: endpoints, request/response JSON shapes, status codes
- [ ] UI/UX: ASCII wireframe for any user-visible change
- [ ] No "TBD" sections

Design decisions are made by the PO agent, informed by the existing architecture in CLAUDE.md files.

**Design invariants** that must never be violated:
- Scoped DbContext — never use AddDbContextPool
- Explicit `.HasColumnName()` for every EF property — no convention
- Anthropic never gets `ChatResponseFormat.Json` — always use `AgentJson.Deserialize<T>()`
- Background work always uses `IServiceScopeFactory` — never inject Scoped services directly
- RLS is automatic — never add WHERE tenant_id manually in queries
- Executive agents are never registered in DI — always `new CeoAgent(client)`

### Stage 4 — Spec

**File naming:** `specs/sprint-N/SN-NNN-feature-name.md` (e.g., `specs/sprint-2/S2-001-meetings.md`)

**Status lifecycle:** `draft → ready → in-progress → done → verified`

Use `specs/_template.md`. The spec must contain:
- Problem statement (why does this exist?)
- Solution overview
- Data model changes (SQL DDL + EF entity)
- API contract (if applicable)
- UI/UX wireframe (if user-visible)
- Acceptance criteria (checkboxes — these are the exit criteria for implementation)
- Test plan (which file to add the test in, what the test verifies)

### Stage 5 — Implementation

**Entry:** Run `/implement-feature specs/sprint-N/SN-NNN-feature-name.md`

The spec is the implementation prompt. Work through the acceptance criteria top-to-bottom. For each criterion:
1. Write the code.
2. Build: `cd src/backend && dotnet build Taim.slnx` (must succeed, 0 errors).
3. Check the box in the spec.

**After all criteria are met:**
1. Set spec status → `done`
2. Update all touched module CLAUDE.md files
3. Update root CLAUDE.md build state table
4. Update METRICS.md
5. Run smoke tests: `dotnet test Taim.Tests/Taim.Tests.csproj`
6. Rebuild + redeploy: `docker compose build taim-api && docker compose up -d taim-api`

### Stage 6 — Review

**Entry:** Run `/review-feature specs/sprint-N/SN-NNN-feature-name.md`

The reviewer reads the spec + the implementation. Checks:
- [ ] Every acceptance criterion is satisfied by the code
- [ ] No invariant violations (see Stage 3 design invariants)
- [ ] No new security issues (SQL injection, XSS, secrets in code)
- [ ] Column mappings are explicit for any new EF entity
- [ ] Notifications use the correct serialization (camelCase HTTP, snake_case SignalR)
- [ ] At least one automated test exists for the feature

**Output:** Append a `## Review` section to the spec:
```markdown
## Review
**Date:** YYYY-MM-DD
**Result:** PASS | FAIL
**Notes:** ...
```

If FAIL: add TODO items to the spec, set status back to `in-progress`.
If PASS: set spec status → `verified`.

### Stage 7 — Documentation & Rollout

1. Update module CLAUDE.md files (already done in Stage 5)
2. Update `docs/user-guide.md` if the feature changes anything the user sees
3. Update `METRICS.md` (sprint progress, test counts)
4. Update Section 7 of this file (sprint status table)
5. Run E2E test if user-visible: `cd src/ui-tests && npx playwright test`
6. Rebuild frontend if needed: `docker compose build taim-web && docker compose up -d taim-web`

---

## 4. Bug Lifecycle

### Reporting

Any agent or the user creates `specs/bugs/BUG-NNN-short-description.md` using `specs/_bug-template.md`.

Required fields:
- Symptom (what the user/agent sees)
- Reproduction steps
- Expected vs actual behavior
- Severity: `critical | high | medium | low`

### Severity Rules

| Severity | Definition | Response |
|---|---|---|
| **Critical** | Data loss, security vulnerability, system down | Fix in current sprint immediately |
| **High** | Core feature broken, no workaround | Fix in next sprint |
| **Medium** | Feature degraded, workaround exists | Fix within 2 sprints |
| **Low** | Cosmetic, minor UX issue | Backlog |

### Fix Process

Same as feature implementation. The bug spec becomes the work item.

**Required:** Add a test that would have caught the bug. Name it with the bug ID:
```csharp
[Fact]
public async Task BUG_003_KickoffDoesNotCrashOnNullDelegation() { ... }
```

### Documentation

After fix, append to the bug spec:
```markdown
## Root Cause
...

## Fix
Files changed: ...
Why it fixes it: ...
```

---

## 5. Agent Skills

Skills live in `.claude/commands/`. Each is a markdown file describing the task. Invoke them with `/skill-name`.

| Command | When to use |
|---|---|
| `/new-feature` | Starting a new feature spec from a problem statement |
| `/implement-feature` | Implementing a feature from an existing spec |
| `/review-feature` | Reviewing an implementation against its spec |
| `/new-bug` | Creating a bug report from a symptom |
| `/update-metrics` | Updating METRICS.md after a sprint |
| `/run-tests` | Running smoke + E2E tests and reporting results |
| `/add-endpoint` | Step-by-step process for adding a new API endpoint |
| `/add-notification` | Adding a new NotificationKind |
| `/add-executive-agent` | Adding a new executive agent role |
| `/deploy` | Rebuilding and redeploying containers |
| `/sprint-status` | Current sprint state and what to build next |

**How prompts are generated for implementation agents:**

The spec file is the prompt. No additional prompt engineering is needed. The implementer agent receives:
```
Implement the feature described in specs/sprint-N/SN-NNN-feature-name.md.
Load the context files listed in PROCESS.md Section 2.
Work through each acceptance criterion in order.
Update CLAUDE.md files and METRICS.md when done.
```

This means **spec quality = implementation quality**. A vague spec produces vague code.

---

## 6. Product Analysis

### How We Determine What to Build

**North Star Metric:** Ratio of user decisions to agent work done. Target: 2–3 user decisions per session, dozens of agent actions. Today we have ~0 agent actions after kickoff (no work loop). The product roadmap is sequenced to close this gap.

**Decision framework for new features:**
1. Does it move us toward self-build capability? → P0
2. Does it make the current experience meaningfully better for the user? → P1
3. Is it something Ross has asked for? → P1 minimum
4. Is it tech infrastructure that enables P0/P1? → P0 if blocking, P2 otherwise

**What we do NOT build:**
- Features without a spec
- Features that are speculative ("might be useful")
- Features that don't have a clear acceptance criterion

### Product Analysis Session

Before each sprint, the PO agent runs a product analysis:
1. Read `METRICS.md` for current state
2. Read `specs/backlog/` for pending items
3. Review the self-build readiness checklist
4. Identify the 3 highest-priority items
5. Write or update their specs to `ready` status
6. Update Section 7 (sprint plan) in this document

---

## 7. Sprint Plan

### Current Sprint

**Sprint 4 — Developer Agents and Tool Infrastructure** (next)
- Goal: Worker agents kick off correctly; web search and ClaudeCode connectors work in Docker; agents can do real work.
- Status: 🟡 Ready
- Spec: `specs/sprint-4/S4-001-developer-agents.md` (status: ready)
- Exit criteria: An executive agent completes a web-search action autonomously; a Developer agent receives a `claude_code` action and the approval gate fires correctly

### Sprint History

| Sprint | Goal | Features | Status | Verified |
|---|---|---|---|---|
| 0 | Foundation | Login, goal submission, team assembly, kickoff, reports, approvals | ✅ Done | ✅ E2E passing |
| 1 | Work items + process | Actions table + API, delegation dispatch, full CLAUDE.md coverage, PROCESS.md | ✅ Done | ✅ Build + smoke |
| 2 | Meetings | kickoff_sync + status_check meetings, meeting viewer in UI | ✅ Done | ✅ 20 smoke tests passing |
| 3 | Agent work loop | ActionWorker, IActionExecutor, ConnectorMapping, POST /execute | ✅ Done | ✅ 24 smoke tests passing |
| 4 | Developer tools | Worker agents, ClaudeCode/WebSearch operational, Docker infra | 🟡 Ready | — |
| 5 | KPI dashboard + audit | /tasks/:id/kpis page, approval decided_at, approval history | 🔲 Backlog | — |
| 6 | Scale + autonomy | Sub-team spawning, scheduling, self-build test | 🔲 Backlog | — |

---

## 8. Self-Build Readiness Gate

The platform is self-build capable when all of these are true:

- [x] Actions table + API (Sprint 1)
- [x] Delegation dispatch from kickoff (Sprint 1)
- [ ] Agent work loop: receive action → execute → complete/block (Sprint 3)
- [x] Meetings: kickoff_sync at minimum (Sprint 2)
- [ ] ClaudeCode connector wired to Developer agents (Sprint 4)
- [ ] Sub-team spawning: CTO can create Developer/QA agents (Sprint 6)

**Test:** Submit _"Build out a team to finish the rest of this platform"_ and verify that:
1. Bootstrap creates CTO + 2 Developer + 1 QA
2. CTO reads codebase, creates work breakdown as Actions
3. Developer agents claim actions, use claude_code to write code
4. QA agent runs tests, reports results
5. User receives a summary to review

---

## 9. Metrics & Reporting

See `METRICS.md` for the live tracker. Updated at the end of each sprint.

**What gets measured:**
- Sprint velocity: features shipped per sprint (target: 3)
- Build health: 0 compile errors at all times
- Test coverage: smoke tests (currently 17), E2E tests (currently 1)
- Bug count: open critical/high bugs (target: 0 critical, ≤2 high)
- Feature completion: % of self-build gate items done (currently 2/6 = 33%)

**How metrics are communicated:**
- `METRICS.md` is the stakeholder artifact — readable by Ross at any time
- The PO agent updates it at the end of every sprint via `/update-metrics`
- Critical bugs are surfaced immediately in the conversation with the user

---

## 10. User Documentation

`docs/user-guide.md` — updated per sprint for user-visible features.

Sections in the user guide:
- Getting started (login, submit goal)
- Understanding the team view (agents, status, graph)
- Actions panel (what actions are, how to read status)
- Activity console
- Reports page
- Approvals queue

Each sprint that ships a user-visible feature appends a new section. The guide is written for a non-technical user who wants to understand what TAIM is doing.
