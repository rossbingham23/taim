---
id: S2-001
title: Agent-to-Agent Meetings
sprint: 2
status: done
created: 2026-05-18
updated: 2026-05-18
implemented: 2026-05-18
---

# S2-001 — Agent-to-Agent Meetings

## Problem Statement

After kickoff, executives have individual strategies but no way to align them. The CEO's delegations go to the CTO as Action records, but the CTO never responds, never asks for clarification, and never reports back. The agents are isolated monologues. There is no coordination layer — which is the most fundamental capability of any team. Without it, TAIM is a parallel report generator, not an organisation.

## Solution Overview

Introduce a structured meeting system where agents conduct turn-based LLM conversations. The **organizer** drives the meeting (sets agenda, decides who speaks next, closes the meeting). **Participants** respond when addressed. Meetings produce a summary and action items. The first meeting type to ship is `kickoff_sync` — triggered automatically after all executives complete their kickoff — which is the CEO bringing the whole team together to align on combined strategy.

Meetings are **fully autonomous** (no user participation). The user sees the completed transcript and action items after the meeting ends via a SignalR notification.

## Data Model

The `meetings`, `meeting_messages`, and `meeting_participants` tables already exist in the DB schema. No migration required.

### Schema (existing)

```sql
meetings (
    id uuid, tenant_id uuid, task_id uuid,
    topic text, meeting_type text, status text,
    organizer_agent_id uuid,
    started_at timestamptz, completed_at timestamptz,
    summary text
)
meeting_participants (meeting_id uuid, agent_id uuid, role text)
meeting_messages (
    id uuid, meeting_id uuid, tenant_id uuid,
    speaker_agent_id uuid, content text,
    sequence int, created_at timestamptz
)
```

`meeting_type` values: `kickoff_sync | status_check | decision_request | escalation | briefing`
`status` values: `in_progress | completed | failed`
`meeting_participants.role` values: `organizer | participant`

### EF Entities

`MeetingEntity`, `MeetingParticipantEntity`, `MeetingMessageEntity` already exist in `Taim.Data/Models/Entities.cs`.

**One missing field:** Add `OrganizerAgentId` to `MeetingEntity` and `CompletedAt` (rename `EndedAt` → `CompletedAt` or add separate field).

Check the existing entity and add:
- `MeetingEntity.OrganizerAgentId` (Guid?) with column `organizer_agent_id`
- `MeetingEntity.TaskId` (Guid?) with column `task_id`
- `MeetingParticipantEntity.Role` (string) with column `role`

Verify these columns exist in `init.sql` before adding to the entity. If missing, add them via `ALTER TABLE` on the live container AND update `init.sql`.

## Core Interface

### New file: `Taim.Core/Meetings/IMeetingOrchestrator.cs`

```csharp
namespace Taim.Core.Meetings;

public enum MeetingType { KickoffSync, StatusCheck, DecisionRequest, Escalation, Briefing }

public sealed record MeetingRecord(
    Guid Id, Guid TenantId, Guid? TaskId,
    string Topic, MeetingType MeetingType, string Status,
    Guid OrganizerAgentId,
    IReadOnlyList<Guid> ParticipantAgentIds,
    string? Summary,
    int MessageCount,
    DateTimeOffset StartedAt, DateTimeOffset? CompletedAt
);

public sealed record StartMeetingRequest(
    Guid TenantId, Guid? TaskId,
    MeetingType MeetingType,
    string Topic,
    Guid OrganizerAgentId,
    IReadOnlyList<Guid> ParticipantAgentIds
);

public interface IMeetingOrchestrator
{
    // Starts and runs a meeting to completion. Returns the completed meeting record.
    Task<MeetingRecord> RunAsync(StartMeetingRequest request, CancellationToken ct = default);
}

public interface IMeetingStore
{
    Task<MeetingRecord> CreateAsync(StartMeetingRequest request, CancellationToken ct = default);
    Task<MeetingRecord> GetAsync(Guid tenantId, Guid meetingId, CancellationToken ct = default);
    Task<IReadOnlyList<MeetingRecord>> GetForTaskAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);
    Task AddMessageAsync(Guid tenantId, Guid meetingId, Guid speakerAgentId, string content, int sequence, CancellationToken ct = default);
    Task CompleteAsync(Guid tenantId, Guid meetingId, string summary, CancellationToken ct = default);
    Task<IReadOnlyList<MeetingMessageRecord>> GetMessagesAsync(Guid tenantId, Guid meetingId, CancellationToken ct = default);
}

public sealed record MeetingMessageRecord(
    Guid Id, Guid MeetingId, Guid? SpeakerAgentId, string Content, int Sequence, DateTimeOffset CreatedAt
);
```

## Agent Implementation

### New file: `Taim.Agents/Meetings/MeetingOrchestrator.cs`

The meeting orchestrator runs a turn-based LLM conversation. It is a Scoped service registered in `AgentsExtensions`.

```
Algorithm:

1. Create meeting record (store.CreateAsync)
2. Log opening notification
3. max_turns = 20
4. turn = 0

LOOP:
  a. Build organizer context (charter, KPIs, active actions, full transcript so far)
  b. Call organizer LLM:
     - If turn == 0: "You are opening this meeting. Topic: {topic}. Introduce the agenda and ask your first question to {participant_name}."
     - If turn > 0: "Continue the meeting. Review the transcript. You may: ask a follow-up, address another participant, or close the meeting. If closing, respond with a JSON closing block."
  c. Parse organizer response:
     - If contains closing signal → go to CLOSE
     - Otherwise → store as message, notify
  d. Identify addressed participant (organizer names them explicitly, or first in list)
  e. Build participant context + transcript
  f. Call participant LLM: "You are in a meeting with {organizer_name}. Respond to: {last_organizer_message}"
  g. Store participant message, notify
  h. turn++
  if turn >= max_turns → CLOSE (organizer generates closing)

CLOSE:
  a. Organizer generates closing: `ExecutiveMeetingClose { summary, actionItems: [{ assigneeRole, title, description }] }`
  b. Parse and create Action records for each action item (assignee matched by role)
  c. store.CompleteAsync(summary)
  d. Push NotificationKind.MeetingCompleted with { meetingId, summary, actionCount }
  e. Log "Meeting complete: {topic} — {summary[:100]}"
```

### LLM Response Schemas

**Organizer turn response** (free text unless closing):
- Free prose is valid
- To close: include `<close>{ "summary": "...", "actionItems": [{ "assigneeRole": "cto", "title": "...", "description": "..." }] }</close>` at the end

**Or use structured JSON for all organizer turns** (simpler to parse):
```json
{
  "message": "The text spoken in the meeting",
  "addressedParticipantRole": "cto",
  "closeMeeting": false,
  "summary": null,
  "actionItems": []
}
```

Use `AgentJson.Deserialize<OrganizerTurn>()` — handles markdown fences and snake_case normalization.

### System Prompts

**Organizer system prompt:**
```
You are {agentName}, {role}, running a {meetingType} meeting.
Topic: {topic}
Participants: {participantList}

Your goal: drive the meeting toward a clear outcome. Ask focused questions. 
When you have enough information, close the meeting with a summary and concrete action items.

Keep each message concise (2–4 sentences). This is a professional meeting, not a report.

{charter}
{teamContext}
{kpiContext}

Respond with JSON:
{
  "message": "what you say in the meeting",
  "addressedParticipantRole": "role of who you're addressing (or null if closing)",
  "closeMeeting": false,
  "summary": null,
  "actionItems": []
}
When closing: set closeMeeting=true, fill summary and actionItems.
```

**Participant system prompt:**
```
You are {agentName}, {role}, in a meeting.
You have been addressed by {organizerName}.

Respond directly to what was said. Be concrete. Keep it brief (2–4 sentences).
You may share your own perspective or ask a clarifying question if essential.

{charter}
{kpiContext}

Meeting transcript so far:
{transcript}

Respond with just your spoken message (no JSON needed — participant turns are free text).
```

## Kickoff Sync Trigger

In `AgentOrchestrator.KickoffTeamAsync`, after `Task.WhenAll(tasks)` completes:

```csharp
// Find the CEO agent (role = ceo or bootstrap)
var ceoAgent = team.FirstOrDefault(m => m.Definition.Role == AgentRole.Ceo);
if (ceoAgent is not null)
{
    var allAgentIds = team.Select(m => m.Definition.Id).ToList();
    var participantIds = allAgentIds.Where(id => id != ceoAgent.Definition.Id).ToList();
    
    // Run in background (non-blocking relative to task status update)
    _ = Task.Run(async () =>
    {
        using var scope = scopeFactory.CreateScope();
        var meetingOrchestrator = scope.ServiceProvider.GetRequiredService<IMeetingOrchestrator>();
        await meetingOrchestrator.RunAsync(new StartMeetingRequest(
            tenantId, taskId, MeetingType.KickoffSync,
            $"Align on combined strategy for: {goal}",
            ceoAgent.Definition.Id, participantIds), ct);
    });
}
```

The meeting runs asynchronously — the task status is updated to `active` immediately, then the meeting happens in the background.

**The `IMeetingOrchestrator` needs `IChatClient` per agent.** The `KickoffTeamAsync` already has `team` which contains `(AgentDefinition, IChatClient)` pairs. Pass a `Dictionary<Guid, IChatClient>` to `MeetingOrchestrator` so it can call the right model for each agent.

`MeetingOrchestrator` constructor:
```csharp
public sealed class MeetingOrchestrator(
    IMeetingStore store,
    IAgentRegistry registry,
    IKpiService kpiService,
    IActionService actionService,
    INotificationService notifications,
    ILogger<MeetingOrchestrator> logger)
```

The `chatClients` dictionary is passed as a parameter to `RunAsync`, not injected — this avoids needing to resolve IChatClient from DI.

## API Contract

### `POST /api/meetings`

Starts a meeting manually (future use — Sprint 2 meetings are triggered automatically).

```json
{
  "taskId": "uuid",
  "meetingType": "statusCheck",
  "topic": "string",
  "organizerAgentId": "uuid",
  "participantAgentIds": ["uuid"]
}
```
Response 202: `{ "meetingId": "uuid" }` (meeting runs async)

### `GET /api/meetings?taskId=`

Returns all meetings for a task (summary view, no messages).

Response 200: `MeetingRecord[]`

### `GET /api/meetings/{id}`

Returns meeting detail including full transcript.

Response 200: `{ meeting: MeetingRecord, messages: MeetingMessageRecord[] }`

### New notification kinds

Add to `NotificationKind` enum:
- `MeetingStarted` — when meeting begins
- `MeetingMessage` — each turn (for live updates — optional in Sprint 2, can batch)
- `MeetingCompleted` — when meeting ends (with summary in metadata)

`MeetingCompleted` metadata:
```json
{
  "meetingId": "uuid",
  "taskId": "uuid",
  "topic": "string",
  "summary": "string",
  "actionItemCount": 3,
  "messageCount": 8
}
```

## UI/UX

### Meetings section in TeamView (below Activity)

```
┌──────────────────────────────────────────────────────────┐
│ MEETINGS (1)                                 [collapse ▼] │
│                                                           │
│ ┌──────────────────────────────────────────────────────┐  │
│ │ Kickoff Sync ✓  8 messages · 3 actions               │  │
│ │ "Align on combined strategy for: Build an AI…"       │  │
│ │                                                [▶ View]│  │
│ └──────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────┘
```

### Meeting Viewer (expandable inline, or modal)

```
┌──────────────────────────────────────────────────────────┐
│ Kickoff Sync                                    [Close ✕] │
│ Started: 12:34 · Completed: 12:37 · 8 messages           │
│ ─────────────────────────────────────────────────────    │
│ [CEO] Victoria Harper                                     │
│ "Let's align on our combined strategy. CTO, what are      │
│  your top 3 technical priorities?"                        │
│                                                           │
│ [CTO] Marcus Chen                                         │
│ "Three priorities: 1) API-first architecture, 2) CI/CD    │
│  from day one, 3) choose the ML stack by end of week."   │
│                                                           │
│ [CEO] Victoria Harper                                     │
│ "Agreed. CMO, how does that timeline work for launch?"   │
│ ─────────────────────────────────────────────────────    │
│ Summary: Team aligned on tech-first approach. CTO owns    │
│ architecture; CMO adjusts launch timeline to week 6.      │
│                                                           │
│ ACTION ITEMS (2):                                         │
│ · Marcus Chen — Finalize tech stack selection by Fri      │
│ · Sarah Kim   — Update launch timeline for stakeholders   │
└──────────────────────────────────────────────────────────┘
```

### Component: `MeetingViewer` (already exists as Storybook component)

Check `src/frontend/taim-web/src/components/MeetingViewer/`. Verify it exists and wire it to real data.

## New Frontend Types

Add to `src/frontend/taim-web/src/lib/types.ts`:

```ts
export type MeetingType = 'kickoff_sync' | 'status_check' | 'decision_request' | 'escalation' | 'briefing'
export type MeetingStatus = 'in_progress' | 'completed' | 'failed'

export interface MeetingRecord {
  id: string
  tenantId: string
  taskId?: string
  topic: string
  meetingType: MeetingType
  status: MeetingStatus
  organizerAgentId: string
  participantAgentIds: string[]
  summary?: string
  messageCount: number
  startedAt: string
  completedAt?: string
}

export interface MeetingMessage {
  id: string
  meetingId: string
  speakerAgentId?: string
  content: string
  sequence: number
  createdAt: string
}
```

Add to `NotificationKind`: `'meeting_started' | 'meeting_message' | 'meeting_completed'`

## New API calls

Add to `src/frontend/taim-web/src/lib/api.ts`:

```ts
export function listMeetings(taskId: string): Promise<MeetingRecord[]>
export function getMeeting(meetingId: string): Promise<{ meeting: MeetingRecord; messages: MeetingMessage[] }>
```

## Acceptance Criteria

- [x] **AC-1**: After `KickoffTeamAsync` completes, a `kickoff_sync` meeting is automatically started in the background
- [x] **AC-2**: The meeting runs to completion (organizer + participants exchange turns, meeting closes with summary)
- [x] **AC-3**: Meeting record, messages, and participants are persisted in the DB
- [x] **AC-4**: Action items from the meeting are created as `ActionRecord` entries in the DB
- [x] **AC-5**: `MeetingCompleted` SignalR notification fires when meeting ends (with summary in metadata)
- [x] **AC-6**: `GET /api/meetings?taskId=` returns the meeting (status=completed after it finishes)
- [x] **AC-7**: `GET /api/meetings/{id}` returns full transcript
- [x] **AC-8**: Meetings section appears in TeamView UI, showing meeting summary and message count
- [x] **AC-9**: Clicking a meeting shows the full transcript in `MeetingViewer`
- [x] **AC-10**: Max 20 turns enforced (meeting auto-closes at limit with whatever summary exists)
- [x] **AC-11**: Meeting failure (LLM error, timeout) is caught and meeting is marked `failed` — does not crash the task

## Test Plan

**Smoke tests** (added to `Taim.E2ETests/MeetingTests.cs`):
- [x] `GET /api/meetings?taskId=` returns 200 with array
- [x] `GET /api/meetings/{id}` returns 200 or 404

**E2E addition** (update `src/ui-tests/tests/user-journey.spec.ts`):
- [ ] After team assembles and activity settles, a "Meetings" section appears with at least one entry

## Implementation Order

1. Check/update `MeetingEntity`, `MeetingParticipantEntity` in `Entities.cs` — add missing fields
2. Update `TaimDbContext` EF config for missing fields
3. Check/update `IMeetingStore` in `Taim.Core` and `MeetingService` in `Taim.Data`
4. Create `Taim.Core/Meetings/IMeetingOrchestrator.cs` with new types
5. Create `Taim.Agents/Meetings/MeetingOrchestrator.cs` implementing the turn loop
6. Register `IMeetingOrchestrator` → `MeetingOrchestrator` in `AgentsExtensions.cs`
7. Update `AgentOrchestrator.KickoffTeamAsync` to fire `kickoff_sync` post-kickoff
8. Create `Taim.Api/Endpoints/MeetingEndpoints.cs`
9. Wire `app.MapMeetingEndpoints()` in `Program.cs`
10. Add `MeetingStarted`, `MeetingCompleted` to `NotificationKind` enum
11. Update frontend types + API client
12. Add Meetings section + `MeetingViewer` to `TeamView.tsx`
13. Update CLAUDE.md files + METRICS.md

## CLAUDE.md Updates Required

- [ ] `Taim.Core/CLAUDE.md` — add `IMeetingOrchestrator`, `MeetingRecord`, new NotificationKinds
- [ ] `Taim.Data/CLAUDE.md` — verify `meetings` table entry is current
- [ ] `Taim.Api/CLAUDE.md` — add meeting endpoints
- [ ] `Taim.Agents/CLAUDE.md` — document `MeetingOrchestrator` and kickoff_sync trigger
- [ ] Root `CLAUDE.md` — update build state

## Review

**Date:** —
**Result:** —
**Notes:** —
