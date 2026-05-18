using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Taim.Agents.Shared;
using Taim.Core.Actions;
using Taim.Core.Agents;
using Taim.Core.KPIs;
using Taim.Core.Meetings;
using Taim.Core.Notifications;

namespace Taim.Agents.Meetings;

public sealed class MeetingOrchestrator(
    IMeetingStore store,
    IAgentRegistry registry,
    IKpiService kpiService,
    IActionService actionService,
    INotificationService notifications,
    ILogger<MeetingOrchestrator> logger) : IMeetingOrchestrator
{
    private const int MaxTurns = 20;

    public async Task<MeetingRecord> RunAsync(
        StartMeetingRequest request,
        Dictionary<Guid, IChatClient> chatClients,
        CancellationToken ct = default)
    {
        var meeting = await store.CreateAsync(request, ct);
        logger.LogInformation("Meeting started: {MeetingId} — {Topic}", meeting.Id, meeting.Topic);

        await notifications.NotifyAsync(meeting.TenantId, NotificationKind.MeetingStarted,
            $"Meeting started: {meeting.Topic}", string.Empty,
            new Dictionary<string, object?>
            {
                ["meetingId"] = meeting.Id.ToString(),
                ["taskId"]    = meeting.TaskId?.ToString(),
                ["topic"]     = meeting.Topic,
            }, ct);

        try
        {
            meeting = await RunLoopAsync(request, meeting, chatClients, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Meeting {MeetingId} failed", meeting.Id);
            await store.FailAsync(meeting.TenantId, meeting.Id, ct);
            throw;
        }

        return meeting;
    }

    private async Task<MeetingRecord> RunLoopAsync(
        StartMeetingRequest request,
        MeetingRecord meeting,
        Dictionary<Guid, IChatClient> chatClients,
        CancellationToken ct)
    {
        var organizerDef  = await registry.GetAsync(meeting.TenantId, request.OrganizerAgentId, ct);
        var participantDefs = new List<AgentDefinition>();
        foreach (var pid in request.ParticipantAgentIds)
        {
            var p = await registry.GetAsync(meeting.TenantId, pid, ct);
            if (p is not null) participantDefs.Add(p);
        }

        if (organizerDef is null || !chatClients.TryGetValue(request.OrganizerAgentId, out var organizerClient))
        {
            logger.LogWarning("Organizer agent {Id} not found — closing meeting immediately", request.OrganizerAgentId);
            await store.CompleteAsync(meeting.TenantId, meeting.Id, "Meeting ended: organizer unavailable.", ct);
            return await store.GetAsync(meeting.TenantId, meeting.Id, ct);
        }

        var transcript = new List<(string Name, string Text)>();
        var sequence = 0;

        // Get team + KPI context for system prompts
        var teamContext = BuildTeamContextAsync(organizerDef, participantDefs);
        var kpiContext  = await BuildKpiContextAsync(meeting.TenantId, organizerDef.Id, ct);

        OrganizerTurn? closingTurn = null;

        for (int turn = 0; turn < MaxTurns; turn++)
        {
            // ── Organizer turn ────────────────────────────────────────────────
            var organizerInstruction = turn == 0
                ? $"You are opening this meeting. Topic: {meeting.Topic}. Introduce the agenda and ask your first question to {participantDefs.FirstOrDefault()?.Name ?? "the team"}."
                : "Continue the meeting. Review the transcript. You may ask a follow-up, address another participant, or close the meeting if you have enough information.";

            var organizerSystem = BuildOrganizerSystem(organizerDef, participantDefs, meeting.Topic, teamContext, kpiContext);
            var organizerPrompt = BuildTranscriptBlock(transcript) + "\n\n" + organizerInstruction;

            var orgMessages = new List<ChatMessage>
            {
                new(ChatRole.System, organizerSystem),
                new(ChatRole.User, organizerPrompt),
            };
            var organizerResponse = await chatClients[request.OrganizerAgentId].GetResponseAsync(orgMessages, null, ct);
            var organizerRaw = organizerResponse.Text ?? string.Empty;

            OrganizerTurn? orgTurn = null;
            try { orgTurn = AgentJson.Deserialize<OrganizerTurn>(organizerRaw, organizerDef.Name); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to parse organizer turn JSON; treating as plain text"); }

            var organizerText = orgTurn?.Message ?? organizerRaw;
            if (string.IsNullOrWhiteSpace(organizerText)) organizerText = "(no message)";

            transcript.Add((organizerDef.Name, organizerText));
            await store.AddMessageAsync(meeting.TenantId, meeting.Id, organizerDef.Id, organizerText, sequence++, ct);
            await notifications.NotifyAsync(meeting.TenantId, NotificationKind.MeetingMessage,
                $"[{organizerDef.Name}] {organizerText[..Math.Min(80, organizerText.Length)]}…",
                string.Empty, new Dictionary<string, object?> { ["meetingId"] = meeting.Id.ToString() }, ct);

            // Check if organizer wants to close
            if (orgTurn?.CloseMeeting == true || turn == MaxTurns - 1)
            {
                closingTurn = orgTurn;
                break;
            }

            // ── Participant turn ──────────────────────────────────────────────
            var addressed = FindAddressedParticipant(orgTurn?.AddressedParticipantRole, participantDefs)
                          ?? participantDefs.ElementAtOrDefault(turn % Math.Max(1, participantDefs.Count));

            if (addressed is null || !chatClients.TryGetValue(addressed.Id, out var participantClient))
                continue;

            var participantKpiContext = await BuildKpiContextAsync(meeting.TenantId, addressed.Id, ct);
            var participantSystem = BuildParticipantSystem(addressed, organizerDef, participantKpiContext);
            var participantPrompt = BuildTranscriptBlock(transcript) + $"\n\nPlease respond to {organizerDef.Name}'s last message.";

            var pMessages = new List<ChatMessage>
            {
                new(ChatRole.System, participantSystem),
                new(ChatRole.User, participantPrompt),
            };
            var participantResponse = await participantClient.GetResponseAsync(pMessages, null, ct);
            var participantText = participantResponse.Text ?? "(no message)";

            transcript.Add((addressed.Name, participantText));
            await store.AddMessageAsync(meeting.TenantId, meeting.Id, addressed.Id, participantText, sequence++, ct);
            await notifications.NotifyAsync(meeting.TenantId, NotificationKind.MeetingMessage,
                $"[{addressed.Name}] {participantText[..Math.Min(80, participantText.Length)]}…",
                string.Empty, new Dictionary<string, object?> { ["meetingId"] = meeting.Id.ToString() }, ct);
        }

        // ── Close meeting ─────────────────────────────────────────────────────
        var summary = closingTurn?.Summary ?? GenerateSummary(meeting.Topic, transcript);
        await store.CompleteAsync(meeting.TenantId, meeting.Id, summary, ct);

        // Dispatch action items
        var actionCount = 0;
        if (closingTurn?.ActionItems is { Count: > 0 })
        {
            foreach (var item in closingTurn.ActionItems)
            {
                if (string.IsNullOrWhiteSpace(item.Title)) continue;
                var assignee = FindAssigneeByRole(item.AssigneeRole, participantDefs, organizerDef);
                try
                {
                    await actionService.CreateAsync(new CreateActionRequest(
                        meeting.TenantId, meeting.TaskId ?? Guid.Empty,
                        assignee?.Id, organizerDef.Id,
                        item.Title, item.Description, 50), ct);
                    actionCount++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to create meeting action item: {Title}", item.Title);
                }
            }
        }

        var completed = await store.GetAsync(meeting.TenantId, meeting.Id, ct);

        await notifications.NotifyAsync(meeting.TenantId, NotificationKind.MeetingCompleted,
            $"Meeting complete: {meeting.Topic}",
            summary[..Math.Min(200, summary.Length)],
            new Dictionary<string, object?>
            {
                ["meetingId"]      = meeting.Id.ToString(),
                ["taskId"]         = meeting.TaskId?.ToString(),
                ["topic"]          = meeting.Topic,
                ["summary"]        = summary,
                ["actionItemCount"] = actionCount,
                ["messageCount"]   = completed.MessageCount,
            }, ct);

        logger.LogInformation("Meeting complete: {MeetingId} — {MessageCount} messages, {Actions} actions",
            meeting.Id, completed.MessageCount, actionCount);

        return completed;
    }

    // ── Prompt builders ───────────────────────────────────────────────────────

    private static string BuildOrganizerSystem(
        AgentDefinition organizer,
        IReadOnlyList<AgentDefinition> participants,
        string topic,
        string teamContext,
        string kpiContext)
    {
        var participantList = string.Join(", ", participants.Select(p => $"{p.Name} ({p.Role})"));
        return $$"""
            You are {{organizer.Name}}, {{organizer.Role}}, running a meeting.
            Topic: {{topic}}
            Participants: {{participantList}}

            Your goal: drive the meeting toward a clear outcome. Ask focused questions.
            When you have enough information, close the meeting with a summary and concrete action items.

            Keep each message concise (2-4 sentences). This is a professional meeting, not a report.

            {{teamContext}}
            {{kpiContext}}

            Charter: {{organizer.Charter ?? "Lead the organisation."}}

            Respond with JSON only:
            {
              "message": "what you say in the meeting",
              "addressedParticipantRole": "role of who you're addressing, or null if closing",
              "closeMeeting": false,
              "summary": null,
              "actionItems": []
            }
            When closing: set closeMeeting=true, fill summary and actionItems as array of objects with assigneeRole, title, description fields.
            """;
    }

    private static string BuildParticipantSystem(
        AgentDefinition participant,
        AgentDefinition organizer,
        string kpiContext)
    {
        return $$"""
            You are {{participant.Name}}, {{participant.Role}}, in a meeting run by {{organizer.Name}}.
            You have been addressed. Respond directly to what was said.
            Be concrete and brief (2-4 sentences). You may share your perspective or ask a clarifying question.

            Charter: {{participant.Charter ?? "Support the team."}}
            {{kpiContext}}

            Respond with just your spoken message (plain text, no JSON).
            """;
    }

    private static string BuildTranscriptBlock(List<(string Name, string Text)> transcript)
    {
        if (transcript.Count == 0) return "Meeting transcript: (none yet)";
        var lines = transcript.Select(t => $"[{t.Name}]: {t.Text}");
        return "Meeting transcript so far:\n" + string.Join("\n\n", lines);
    }

    private static string GenerateSummary(string topic, List<(string Name, string Text)> transcript)
    {
        var speakers = transcript.Select(t => t.Name).Distinct().ToList();
        return $"Meeting on '{topic}' concluded with {transcript.Count} messages from {string.Join(", ", speakers)}.";
    }

    private static AgentDefinition? FindAddressedParticipant(
        string? role, IReadOnlyList<AgentDefinition> participants)
    {
        if (string.IsNullOrWhiteSpace(role)) return null;
        return participants.FirstOrDefault(p =>
            p.Role.ToString().Equals(role, StringComparison.OrdinalIgnoreCase));
    }

    private static AgentDefinition? FindAssigneeByRole(
        string? role,
        IReadOnlyList<AgentDefinition> participants,
        AgentDefinition organizer)
    {
        if (string.IsNullOrWhiteSpace(role)) return null;
        var all = participants.Prepend(organizer);
        return all.FirstOrDefault(p => p.Role.ToString().Equals(role, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildTeamContextAsync(
        AgentDefinition organizer,
        IReadOnlyList<AgentDefinition> participants)
    {
        var lines = new List<string> { "Meeting participants:" };
        lines.Add($"  Organizer: {organizer.Name} ({organizer.Role})");
        foreach (var p in participants)
            lines.Add($"  - {p.Name} ({p.Role})");
        return string.Join("\n", lines);
    }

    private async Task<string> BuildKpiContextAsync(Guid tenantId, Guid agentId, CancellationToken ct)
    {
        try
        {
            var kpis = await kpiService.GetForAgentAsync(tenantId, agentId, ct);
            if (kpis.Count == 0) return string.Empty;
            var names = kpis.Select(k => k.Name).Take(5);
            return "Your KPIs: " + string.Join(", ", names);
        }
        catch { return string.Empty; }
    }
}

// ── LLM response schemas ──────────────────────────────────────────────────────

internal sealed record OrganizerTurn(
    string? Message,
    string? AddressedParticipantRole,
    bool CloseMeeting,
    string? Summary,
    IReadOnlyList<ActionItem>? ActionItems
);

internal sealed record ActionItem(
    string? AssigneeRole,
    string? Title,
    string? Description
);
