---
id: BL-005
title: Agent Scheduling
sprint: 6
status: draft
created: 2026-05-18
updated: 2026-05-18
---

# BL-005 — Agent Scheduling

## Problem Statement

Agents need to do recurring work: weekly status check meetings, nightly KPI reviews, daily action queue processing. Currently nothing happens unless triggered by kickoff or a user action. Without scheduling, the platform is reactive rather than autonomous.

## Solution Overview

A `scheduled_tasks` table already exists. A worker in `Taim.Host` evaluates cron expressions and fires agent work items on schedule. The user can configure recurring tasks per agent via the settings UI.

## Key Design

**Scheduler worker** (in `Taim.Host`):
- Runs every minute
- Reads `scheduled_tasks` where `next_run_at <= now() AND status = 'active'`
- For each due task: creates an `Action` record assigned to the agent, updates `last_run_at` and `next_run_at` (next cron fire time)

**Built-in schedules (auto-created at kickoff):**
- CEO: `0 9 * * 1` (Monday 9am) → `status_check` meeting with each executive
- All agents: `0 8 * * *` (daily 8am) → "Review your KPIs and update status"

**User-configured:** Settings page allows adding custom cron schedules for any agent.

## Dependencies

- Agent work loop (BL-001)
- `Taim.Host` worker infrastructure (already scaffolded)
- `scheduled_tasks` table already exists

## Spec Status: Draft
