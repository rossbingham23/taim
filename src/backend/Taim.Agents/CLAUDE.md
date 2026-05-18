# Taim.Agents — Agent Architecture

## Executive Agent Pattern

All executive agents extend `ExecutiveAgentBase` (in `Executive/ExecutiveAgentBase.cs`):

```csharp
public abstract class ExecutiveAgentBase(IChatClient chatClient)
{
    protected abstract string RoleTitle { get; }
    protected abstract string RoleDescription { get; }

    // Runs one agent turn; returns structured response
    public Task<ExecutiveResponse> RunAsync(ExecutiveContext ctx, string instruction, ct)

    // Proposes 3-5 KPIs for this agent's role
    public Task<IReadOnlyList<ProposedKpi>> ProposeKpisAsync(ExecutiveContext ctx, ct)

    // Proposes sub-team agents to report to this agent
    public Task<IReadOnlyList<AgentSpec>> ProposeSubTeamAsync(ExecutiveContext ctx, ct)
}
```

All three methods use `AgentJson.Deserialize<T>()` — never `JsonSerializer.Deserialize` directly.

## ExecutiveContext

```csharp
record ExecutiveContext(
    Guid TenantId, Guid AgentId, string AgentName, string Role, string Charter,
    string Goal,
    IReadOnlyList<string> ParentKpis,   // KPIs from the principal/manager
    IReadOnlyList<string> OwnKpis,      // this agent's own KPIs (after proposal)
    string? TeamContext                  // formatted direct reports / peers / manager
);
```

## Executive Agents (NOT in DI)

Instantiated directly — never `sp.GetRequiredService<CeoAgent>()`:

| File | Role |
|---|---|
| `Executive/CeoAgent.cs` | `AgentRole.Ceo` |
| `Executive/CtoAgent.cs` | `AgentRole.Cto` |
| `Executive/CmoAgent.cs` | `AgentRole.Cmo` |
| `Executive/CfoAgent.cs` | `AgentRole.Cfo` |
| `Executive/HrAgent.cs` | `AgentRole.Hr` |

Add a new executive agent by: creating `Executive/XxxAgent.cs`, adding the `AgentRole.Xxx` case in `AgentOrchestrator.InstantiateAgent` and `AgentFactory.MapRole`.

## Other Agents (in DI)

| Class | Registration | Purpose |
|---|---|---|
| `BootstrapAgent` | `AddScoped` | Analyses goal → produces `TeamRecommendation` |
| `ExpertAgent` | `AddScoped` | Gathers domain knowledge to inform bootstrap |

## AgentJson (Shared/AgentJson.cs)

Internal helper. Handles all LLM JSON parsing:
1. Strips ` ```json ` / ` ``` ` markdown fences
2. Normalizes snake_case object keys to camelCase recursively
3. Deserializes with `JsonSerializerDefaults.Web`

```csharp
// Usage:
var result = AgentJson.Deserialize<ExecutiveResponse>(response.Text, "CeoAgent");
```

Throws `JsonException` with the agent name in the message on failure (caught upstream and logged).

## MeetingOrchestrator (Meetings/MeetingOrchestrator.cs)

Scoped service registered via `IMeetingOrchestrator`. Runs a turn-based LLM meeting.

**`RunAsync(request, chatClients, ct)`** — algorithm:
1. `store.CreateAsync` → persist meeting + participants
2. Push `MeetingStarted` notification
3. Loop up to 20 turns:
   - Organizer LLM turn: receives transcript + instruction, returns `OrganizerTurn` JSON
   - If `closeMeeting=true` or turn limit reached → close
   - Addressed participant LLM turn: free-text response
   - Both messages stored via `store.AddMessageAsync`, `MeetingMessage` notifications pushed
4. `store.CompleteAsync(summary)` → status = `completed`
5. Dispatch `actionItems` as `ActionRecord` entries
6. Push `MeetingCompleted` notification with metadata (summary, actionItemCount, messageCount)
- On exception: `store.FailAsync` → status = `failed`

`chatClients` is a `Dictionary<Guid, IChatClient>` passed as parameter (not DI-injected) to use per-agent budget-wrapped clients.

## AgentOrchestrator (Shared/AgentOrchestrator.cs)

Scoped service. Called from `TaskEndpoints` after team creation.

**`KickoffTeamAsync`** — fires all agents in `Task.WhenAll`; individual failures are caught and logged, the rest continue. **After `Task.WhenAll` completes**, fires a background `kickoff_sync` meeting via `IMeetingOrchestrator.RunAsync` with CEO as organizer and all other agents as participants.

**`KickoffAgentAsync`** sequence per agent:
1. `registry.UpdateStatusAsync` → `Active`; push `AgentStatusChanged` notification
2. Build `ExecutiveContext` via `IAgentRegistry` + `IKpiService`
3. `ProposeKpisAsync` → save each KPI via `IKpiService.CreateAsync`
4. `RunAsync(ctx, kickoffInstruction)` → `ExecutiveResponse`
5. `reportService.SaveAsync` → persist `ExecutiveReportEntity`
6. Push `ExecutiveReport` SignalR notification (metadata contains full report)
7. **Delegation dispatch**: for each `response.Delegations` entry, match to a direct report by role/name substring, create `ActionRecord` via `IActionService.CreateAsync`, push `ActionCreated` notification
8. `registry.UpdateStatusAsync` → `Idle`; push `AgentStatusChanged` notification

## AgentFactory (Shared/AgentFactory.cs)

Scoped service. Creates agent DB record + budget-wrapped `IChatClient`:
- Registers agent in `IAgentRegistry`
- Resolves provider config via `IProviderFactory`
- Wraps `IChatClient` with `TokenLedgerMiddleware` (budget tracking)
- Pushes `TeamUpdate` SignalR notification

## System Prompt Rules

- Never include `ChatResponseFormat.Json` in `ChatOptions` — Anthropic rejects it
- Always include explicit JSON structure (field names + example values) in the system prompt
- Use `AgentJson.Deserialize<T>()` for all LLM response parsing
