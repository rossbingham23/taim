using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Taim.Agents.Shared;
using Taim.Core.Actions;
using Taim.Core.Agents;
using Taim.Core.System;
using Taim.Core.Teams;

namespace Taim.Api.Background;

public sealed class AgentScheduler(
    IServiceScopeFactory scopeFactory,
    IOptions<SchedulerOptions> options,
    ILogger<AgentScheduler> logger) : BackgroundService
{
    private static readonly HashSet<string> TriggerableStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "open", "blocked" };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.IntervalSeconds);
        logger.LogInformation("AgentScheduler started; interval={Interval}s", options.Value.IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(interval, stoppingToken);
            await RunTickAsync(stoppingToken);
        }
    }

    private async Task RunTickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        try
        {
            var systemStop = sp.GetRequiredService<ISystemStopService>();
            if (await systemStop.IsStoppedAsync(ct))
            {
                logger.LogDebug("AgentScheduler tick skipped — system stop active");
                return;
            }

            var taskService   = sp.GetRequiredService<ITaskService>();
            var actionService = sp.GetRequiredService<IActionService>();
            var agentRegistry = sp.GetRequiredService<IAgentRegistry>();
            var executor      = sp.GetRequiredService<IActionExecutor>();

            var activeTasks = await taskService.GetSchedulerCandidatesAsync(ct);

            var triggered = 0;
            foreach (var task in activeTasks)
            {
                var actions = await actionService.GetForTaskAsync(task.TenantId, task.Id, ct);
                foreach (var action in actions)
                {
                    if (!TriggerableStatuses.Contains(action.Status)) continue;
                    if (!action.AgentId.HasValue) continue;

                    var agent = await agentRegistry.GetAsync(task.TenantId, action.AgentId.Value, ct);
                    if (agent is null || agent.Status != AgentStatus.Idle) continue;

                    var fired = await executor.TriggerAsync(task.TenantId, action.Id, ct);
                    if (fired) triggered++;
                }
            }

            if (triggered > 0)
                logger.LogInformation("AgentScheduler tick: triggered {Count} action(s)", triggered);
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (Exception ex)
        {
            logger.LogError(ex, "AgentScheduler tick failed");
        }
    }
}
