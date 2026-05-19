using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Taim.Connectors.Sdk;
using Taim.Core.Actions;
using Taim.Core.Agents;
using Taim.Core.Approvals;
using Taim.Core.Memory;
using Taim.Core.Notifications;
using Taim.Core.Providers;
using Taim.Core.System;

namespace Taim.Agents.Shared;

/// <summary>
/// Scoped service — resolves dependencies for a single action's work loop and fires it
/// in a background Task via IServiceScopeFactory so the loop outlives the request scope.
/// </summary>
public sealed class ActionExecutor(
    IServiceScopeFactory scopeFactory,
    IActionService actionService,
    ITaskCancellationRegistry taskCancellationRegistry,
    ILogger<ActionExecutor> logger) : IActionExecutor
{
    private static readonly HashSet<string> TriggerableStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "open", "blocked" };

    public async Task<bool> TriggerAsync(Guid tenantId, Guid actionId, CancellationToken ct = default)
    {
        var action = await actionService.GetAsync(tenantId, actionId, ct);
        if (action is null) return false;
        if (!TriggerableStatuses.Contains(action.Status)) return false;

        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            try
            {
                var actionSvc     = sp.GetRequiredService<IActionService>();
                var agentRegistry = sp.GetRequiredService<IAgentRegistry>();
                var approvalSvc   = sp.GetRequiredService<IApprovalService>();
                var notifications = sp.GetRequiredService<INotificationService>();
                var chatHistory   = sp.GetRequiredService<IChatHistoryProvider>();
                var connectors    = sp.GetRequiredService<IConnectorRegistry>();
                var providerFactory = sp.GetRequiredService<IProviderFactory>();

                // Reload action inside the new scope (avoids stale data)
                var freshAction = await actionSvc.GetAsync(tenantId, actionId, ct);
                if (freshAction is null || !TriggerableStatuses.Contains(freshAction.Status)) return;

                if (!freshAction.AgentId.HasValue)
                {
                    logger.LogWarning("Action {ActionId} has no assigned agent — skipping execution", actionId);
                    return;
                }

                var agent = await agentRegistry.GetAsync(tenantId, freshAction.AgentId.Value, ct);
                if (agent is null)
                {
                    logger.LogWarning("Agent {AgentId} not found for action {ActionId}", freshAction.AgentId, actionId);
                    return;
                }

                var chatClient = providerFactory.CreateChatClient(tenantId, agent.Provider, agent.Model);

                var connectorIds = ConnectorMapping.GetConnectorIds(agent.Role);
                var tools = await connectors.GetToolsForAgentAsync(connectorIds, ct);

                var systemStop = sp.GetRequiredService<ISystemStopService>();
                var scopedLogger = sp.GetRequiredService<ILogger<ActionWorker>>();
                var worker = new ActionWorker(
                    actionSvc, approvalSvc, notifications,
                    agentRegistry, chatHistory, systemStop, scopedLogger);

                var taskToken = taskCancellationRegistry.Register(freshAction.TaskId);
                await worker.ExecuteAsync(tenantId, freshAction.TaskId, freshAction, agent, chatClient, tools, taskToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ActionExecutor background loop failed for action {ActionId}", actionId);
            }
        });

        return true;
    }
}
