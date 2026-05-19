using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Taim.Agents.Executive;
using Taim.Core.Actions;
using Taim.Core.Agents;
using Taim.Core.KPIs;
using Taim.Core.Meetings;
using Taim.Core.Notifications;
using Taim.Core.Reports;

namespace Taim.Agents.Shared;

public sealed class AgentOrchestrator(
    IServiceScopeFactory scopeFactory,
    ILogger<AgentOrchestrator> logger)
{
    private const string KickoffInstruction =
        "You have just been activated as part of a new executive team. " +
        "Execute your charter kickoff: outline your strategic analysis, state your key decisions and priorities, " +
        "and list your first concrete actions. Be specific and decisive.";

    public async Task KickoffTeamAsync(
        Guid tenantId,
        Guid taskId,
        string goal,
        IReadOnlyList<(AgentDefinition Definition, IChatClient ChatClient)> team,
        CancellationToken ct = default)
    {
        // Each agent gets its own DI scope and TaimDbContext to avoid EF concurrency errors.
        var tasks = team.Select(member =>
            SafeKickoffAsync(tenantId, taskId, goal, member.Definition, member.ChatClient, ct));

        await Task.WhenAll(tasks);

        // Fire work loops for all agents that have open actions assigned to them
        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            try
            {
                var actionService = sp.GetRequiredService<IActionService>();
                var executor = sp.GetRequiredService<IActionExecutor>();
                var allActions = await actionService.GetForTaskAsync(tenantId, taskId, ct);
                foreach (var action in allActions.Where(a => a.Status == "open" && a.AgentId.HasValue))
                {
                    try { await executor.TriggerAsync(tenantId, action.Id, ct); }
                    catch (Exception ex) { logger.LogError(ex, "Failed to trigger action {ActionId}", action.Id); }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start action work loops for task {TaskId}", taskId);
            }
        }, ct);

        // Fire kickoff_sync meeting in background after all agents complete kickoff
        var ceoMember = team.FirstOrDefault(m => m.Definition.Role == AgentRole.Ceo);
        if (ceoMember.Definition is not null)
        {
            var chatClients = team.ToDictionary(m => m.Definition.Id, m => m.ChatClient);
            var participantIds = team
                .Where(m => m.Definition.Id != ceoMember.Definition.Id)
                .Select(m => m.Definition.Id)
                .ToList();

            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var meetingOrchestrator = scope.ServiceProvider.GetRequiredService<IMeetingOrchestrator>();
                try
                {
                    await meetingOrchestrator.RunAsync(
                        new StartMeetingRequest(
                            tenantId, taskId, MeetingType.KickoffSync,
                            $"Align on combined strategy for: {goal}",
                            ceoMember.Definition.Id, participantIds),
                        chatClients, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "kickoff_sync meeting failed for task {TaskId}", taskId);
                }
            }, ct);
        }
    }

    private async Task SafeKickoffAsync(
        Guid tenantId, Guid taskId, string goal,
        AgentDefinition definition, IChatClient chatClient, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var sp            = scope.ServiceProvider;
        var registry      = sp.GetRequiredService<IAgentRegistry>();
        var notifications = sp.GetRequiredService<INotificationService>();

        try
        {
            if (IsWorkerRole(definition.Role))
            {
                await WorkerKickoffAsync(registry, notifications, tenantId, taskId, definition, ct);
            }
            else
            {
                var kpiService    = sp.GetRequiredService<IKpiService>();
                var reportService = sp.GetRequiredService<IReportService>();
                var actionService = sp.GetRequiredService<IActionService>();
                await KickoffAgentAsync(
                    registry, kpiService, reportService, notifications, actionService,
                    tenantId, taskId, goal, definition, chatClient, [], ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Kickoff failed for agent {AgentId} ({Name})", definition.Id, definition.Name);

            await Log(notifications, tenantId, taskId, definition,
                $"{definition.Name}: kickoff failed — {ex.Message}", ct);

            try { await registry.UpdateStatusAsync(tenantId, definition.Id, AgentStatus.Idle, ct); }
            catch { /* best-effort status reset */ }
        }
    }

    private async Task KickoffAgentAsync(
        IAgentRegistry registry,
        IKpiService kpiService,
        IReportService reportService,
        INotificationService notifications,
        IActionService actionService,
        Guid tenantId,
        Guid taskId,
        string goal,
        AgentDefinition definition,
        IChatClient chatClient,
        IReadOnlyList<string> parentKpis,
        CancellationToken ct)
    {
        logger.LogInformation("Starting kickoff for {Name} ({Role})", definition.Name, definition.Role);

        var meta = BaseMetadata(tenantId, taskId, definition);

        await registry.UpdateStatusAsync(tenantId, definition.Id, AgentStatus.Active, ct);
        await notifications.NotifyAsync(tenantId, NotificationKind.AgentStatusChanged,
            $"{definition.Name} activated", string.Empty, meta, ct);

        var agent = InstantiateAgent(definition.Role, chatClient);

        // Build initial context (own KPIs empty until proposal runs)
        var ctx = await BuildContextAsync(registry, kpiService, tenantId, goal, definition, parentKpis, [], ct);

        // Step: propose KPIs
        await Log(notifications, tenantId, taskId, definition, $"{definition.Name}: proposing KPIs…", ct);
        var proposed = await agent.ProposeKpisAsync(ctx, ct);

        var ownKpiNames = new List<string>();
        foreach (var kpi in proposed)
        {
            if (string.IsNullOrWhiteSpace(kpi.Name)) continue; // LLM occasionally omits name
            await kpiService.CreateAsync(new CreateKpiRequest(
                tenantId, definition.Id, null,
                kpi.Name, kpi.Description, kpi.TargetValue, kpi.Unit, ParseDirection(kpi.Direction)), ct);
            ownKpiNames.Add(kpi.Name);
        }
        await Log(notifications, tenantId, taskId, definition,
            $"{definition.Name}: {ownKpiNames.Count} KPI(s) saved — {string.Join(", ", ownKpiNames)}", ct);

        // Step: run kickoff strategy
        await Log(notifications, tenantId, taskId, definition, $"{definition.Name}: running kickoff strategy…", ct);
        ctx = await BuildContextAsync(registry, kpiService, tenantId, goal, definition, parentKpis, ownKpiNames, ct);
        var response = await agent.RunAsync(ctx, KickoffInstruction, ct);

        // Persist report
        var content = FormatReport(response);
        var title = $"{definition.Name} — Kickoff Strategy";
        var report = await reportService.SaveAsync(
            new SaveReportRequest(tenantId, taskId, definition.Id, definition.Name, title, content), ct);

        // Push report notification (frontend Reports page reads this)
        await notifications.NotifyAsync(tenantId, NotificationKind.ExecutiveReport, title, string.Empty,
            new Dictionary<string, object?>(meta)
            {
                ["id"]          = report.Id.ToString(),
                ["agentName"]   = definition.Name,
                ["title"]       = title,
                ["content"]     = content,
                ["generatedAt"] = report.GeneratedAt.ToString("O"),
            }, ct);

        // Dispatch delegations as Action records assigned to direct reports
        if (response.Delegations is { Count: > 0 })
        {
            var directReports = await registry.GetTeamAsync(tenantId, definition.Id, ct);
            foreach (var delegation in response.Delegations)
            {
                if (string.IsNullOrWhiteSpace(delegation)) continue;
                var assignee = FindDelegateAgent(directReports, delegation);
                try
                {
                    var action = await actionService.CreateAsync(new CreateActionRequest(
                        tenantId, taskId, assignee?.Id, definition.Id,
                        delegation, null, 50), ct);

                    await notifications.NotifyAsync(tenantId, NotificationKind.ActionCreated,
                        delegation, string.Empty,
                        new Dictionary<string, object?>(meta)
                        {
                            ["actionId"]   = action.Id.ToString(),
                            ["assigneeId"] = assignee?.Id.ToString(),
                        }, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to dispatch delegation as action: {Delegation}", delegation);
                }
            }
            await Log(notifications, tenantId, taskId, definition,
                $"{definition.Name}: dispatched {response.Delegations.Count} delegation(s) as actions", ct);
        }

        await registry.UpdateStatusAsync(tenantId, definition.Id, AgentStatus.Idle, ct);
        await notifications.NotifyAsync(tenantId, NotificationKind.AgentStatusChanged,
            $"{definition.Name} idle", string.Empty, meta, ct);

        await Log(notifications, tenantId, taskId, definition, $"{definition.Name}: kickoff complete", ct);
        logger.LogInformation("Kickoff complete for {Name}", definition.Name);
    }

    private static bool IsWorkerRole(AgentRole role) => role switch
    {
        AgentRole.Ceo or AgentRole.Cto or AgentRole.Cmo
        or AgentRole.Cfo or AgentRole.Hr => false,
        _ => true
    };

    private async Task WorkerKickoffAsync(
        IAgentRegistry registry,
        INotificationService notifications,
        Guid tenantId, Guid taskId,
        AgentDefinition definition,
        CancellationToken ct)
    {
        var meta = BaseMetadata(tenantId, taskId, definition);

        await registry.UpdateStatusAsync(tenantId, definition.Id, AgentStatus.Active, ct);
        await notifications.NotifyAsync(tenantId, NotificationKind.AgentStatusChanged,
            $"{definition.Name} activated", string.Empty, meta, ct);

        await Log(notifications, tenantId, taskId, definition,
            $"{definition.Name}: worker agent ready, waiting for action assignments.", ct);

        await registry.UpdateStatusAsync(tenantId, definition.Id, AgentStatus.Idle, ct);
        await notifications.NotifyAsync(tenantId, NotificationKind.AgentStatusChanged,
            $"{definition.Name} idle", string.Empty, meta, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AgentDefinition? FindDelegateAgent(IReadOnlyList<AgentDefinition> reports, string delegation)
    {
        if (reports.Count == 0) return null;
        var lower = delegation.ToLowerInvariant();
        foreach (var report in reports)
        {
            if (lower.Contains(report.Role.ToString().ToLowerInvariant()))
                return report;
            if (lower.Contains(report.Name.ToLowerInvariant()))
                return report;
        }
        return null;
    }

    private static Dictionary<string, object?> BaseMetadata(Guid tenantId, Guid taskId, AgentDefinition def) =>
        new()
        {
            ["taskId"]  = taskId.ToString(),
            ["agentId"] = def.Id.ToString(),
        };

    private static Task Log(
        INotificationService notifications,
        Guid tenantId, Guid taskId, AgentDefinition def,
        string message, CancellationToken ct) =>
        notifications.NotifyAsync(tenantId, NotificationKind.AgentLog, message, string.Empty,
            BaseMetadata(tenantId, taskId, def), ct);

    private static async Task<ExecutiveContext> BuildContextAsync(
        IAgentRegistry registry,
        IKpiService kpiService,
        Guid tenantId,
        string goal,
        AgentDefinition def,
        IReadOnlyList<string> parentKpis,
        IReadOnlyList<string> ownKpis,
        CancellationToken ct)
    {
        var teamLines = new List<string>();

        var reports = await registry.GetTeamAsync(tenantId, def.Id, ct);
        if (reports.Count > 0)
        {
            teamLines.Add("Your direct reports:");
            foreach (var r in reports)
                teamLines.Add($"  - {r.Name} ({r.Role})");
        }

        if (def.ParentAgentId.HasValue)
        {
            var peers    = await registry.GetTeamAsync(tenantId, def.ParentAgentId, ct);
            var siblings = peers.Where(p => p.Id != def.Id).ToList();
            if (siblings.Count > 0)
            {
                teamLines.Add("Your peers:");
                foreach (var p in siblings)
                    teamLines.Add($"  - {p.Name} ({p.Role})");
            }

            var manager = await registry.GetAsync(tenantId, def.ParentAgentId.Value, ct);
            if (manager is not null)
                teamLines.Add($"You report to: {manager.Name} ({manager.Role})");
        }

        return new ExecutiveContext(
            tenantId, def.Id, def.Name,
            def.Role.ToString(), def.Charter ?? string.Empty,
            goal, parentKpis, ownKpis,
            teamLines.Count > 0 ? string.Join("\n", teamLines) : null);
    }

    private static string FormatReport(ExecutiveResponse r)
    {
        var actions = r.Actions is { Count: > 0 }
            ? string.Join("\n", r.Actions.Select((a, i) => $"{i + 1}. {a}"))
            : "None specified.";

        var delegations = r.Delegations is { Count: > 0 }
            ? string.Join("\n", r.Delegations.Select(d => $"- {d}"))
            : "None.";

        return $"""
            ## Analysis

            {r.Analysis ?? string.Empty}

            ## Decision

            {r.Decision ?? string.Empty}

            ## Actions

            {actions}

            ## Delegations

            {delegations}
            """;
    }

    private static KpiDirection ParseDirection(string? direction) =>
        direction?.ToLowerInvariant().Replace("_", "").Replace(" ", "") switch
        {
            "higherbetter" or "higherisbetter" or "higher" => KpiDirection.HigherIsBetter,
            "lowerbetter" or "lowerisbetter" or "lower"    => KpiDirection.LowerIsBetter,
            "target" or "targetvalue"                       => KpiDirection.TargetValue,
            _ => KpiDirection.HigherIsBetter
        };

    private static ExecutiveAgentBase InstantiateAgent(AgentRole role, IChatClient client) =>
        role switch
        {
            AgentRole.Ceo => new CeoAgent(client),
            AgentRole.Cto => new CtoAgent(client),
            AgentRole.Cmo => new CmoAgent(client),
            AgentRole.Cfo => new CfoAgent(client),
            AgentRole.Hr  => new HrAgent(client),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role,
                "No executive agent implementation for this role."),
        };
}
