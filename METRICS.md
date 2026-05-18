# TAIM — Platform Metrics

> Stakeholder artifact. Updated at the end of each sprint by the PO agent.
> Last updated: 2026-05-18 (Sprint 2 complete)

---

## North Star Metric

**Goal:** 2–3 user decisions per session → dozens of agent actions completed.

| Today | Target |
|---|---|
| User submits goal → agents produce strategy reports and dispatch work-item Actions. Agents then go Idle. No work is executed automatically. | Agents receive Actions, execute them using tools (code writing, web search), report results, request human approval only for consequential decisions. |

**Qualitative assessment:** 🟡 Foundation complete. Work loop missing. Self-build not yet possible.

---

## Self-Build Readiness

| Gate Item | Status | Sprint |
|---|---|---|
| Actions table + API | ✅ Done | Sprint 1 |
| Delegation dispatch from kickoff | ✅ Done | Sprint 1 |
| Agent-to-agent meetings (kickoff_sync) | ✅ Done | Sprint 2 |
| Agent work loop (execute actions + tools) | 🔲 Not started | Sprint 3 |
| ClaudeCode connector → Developer agents | 🔲 Not started | Sprint 4 |
| Sub-team spawning (CTO → Developer/QA) | 🔲 Not started | Sprint 6 |

**Completion: 3 / 6 (50%)**

---

## Sprint Progress

| Sprint | Goal | Features | Status |
|---|---|---|---|
| 0 | Foundation | Login, goals, team assembly, kickoff, reports, approvals, SignalR | ✅ Done |
| 1 | Work items | Actions table + API, delegation dispatch, full module docs, PROCESS.md | ✅ Done |
| 2 | Meetings | Agent-to-agent meetings (kickoff_sync, status_check) | ✅ Done |
| 3 | Work loop | ExecuteActionAsync, tool invocation, action status flow | 🔲 Backlog |
| 4 | Dev tools | ClaudeCode + WebSearch wired to agents | 🔲 Backlog |
| 5 | KPI + audit | KPI dashboard page, approval audit trail | 🔲 Backlog |
| 6 | Autonomy | Sub-team spawning, scheduling, self-build test | 🔲 Backlog |

---

## Feature Completion

| Feature Area | Status |
|---|---|
| Authentication (JWT, tenant isolation) | ✅ Complete |
| Goal submission + team assembly | ✅ Complete |
| Executive agent kickoff (KPIs + strategy reports) | ✅ Complete |
| Real-time notifications (SignalR) | ✅ Complete |
| Activity feed | ✅ Complete |
| Approval queue (tool gates) | ✅ Complete |
| Work-item Actions (create, list, update) | ✅ Sprint 1 |
| Delegation → Actions dispatch | ✅ Sprint 1 |
| Agent-to-agent meetings | ✅ Sprint 2 |
| Agent work loop | 🔲 Sprint 3 |
| Tool use (ClaudeCode, WebSearch) | 🔲 Sprint 4 |
| KPI dashboard page | 🔲 Sprint 5 |
| Approval audit trail | 🔲 Sprint 5 |
| Sub-team spawning | 🔲 Sprint 6 |
| Scheduling | 🔲 Sprint 6 |

---

## Test Health

| Metric | Value | Last Run |
|---|---|---|
| Smoke tests (Taim.E2ETests) | 20 tests | 2026-05-18 ✅ |
| E2E tests (Playwright) | 1 test | 2026-05-18 ✅ |
| Build errors | 0 | 2026-05-18 ✅ |
| TypeScript errors | 0 | 2026-05-18 ✅ |

**E2E coverage:** Login → submit goal → team assembly (up to 3 min) → activity feed → console → goals list.

**Missing test coverage:**
- Actions API (no smoke test yet — add to `Taim.E2ETests`)
- Delegation dispatch (no assertion that actions are created post-kickoff)
- Meeting conversation content (smoke tests verify endpoints, not LLM turn quality)

---

## Build Health

| Component | Last Build | Status |
|---|---|---|
| `Taim.slnx` (.NET 10) | 2026-05-18 (Sprint 2) | ✅ 0 errors, 6 warnings (pre-existing EF version conflict) |
| `taim-web` (React/Vite) | 2026-05-18 (Sprint 2) | ✅ Clean |
| Docker `taim-api` | 2026-05-18 (Sprint 2) | ✅ Running |
| Docker `taim-web` | 2026-05-18 (Sprint 2) | ✅ Running |

---

## Known Issues (Open Bugs)

| ID | Severity | Summary | Sprint |
|---|---|---|---|
| — | — | No open bugs | — |

---

## Sprint Velocity

| Sprint | Planned Features | Shipped Features | Notes |
|---|---|---|---|
| 0 | 8 | 8 | Foundation build |
| 1 | 2 | 2 + docs overhaul | Actions + delegation + full CLAUDE.md coverage |
| 2 | 1 | 1 (MeetingOrchestrator + endpoints + UI) | kickoff_sync meetings, MeetingViewer, 3 smoke tests |
