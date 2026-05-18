using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Taim.Agents.Bootstrap;
using Taim.Agents.Expert;
using Taim.Agents.Shared;
using Taim.Core.Agents;
using Taim.Core.Teams;

namespace Taim.Api.Endpoints;

public static class TaskEndpoints
{
    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tasks")
                       .RequireAuthorization()
                       .WithTags("Tasks");

        group.MapPost("/", SubmitTask).WithName("SubmitTask");
        group.MapGet("/", ListTasks).WithName("ListTasks");
        group.MapGet("/{taskId:guid}", GetTask).WithName("GetTask");

        return app;
    }

    private static async Task<IResult> SubmitTask(
        [FromBody] SubmitTaskRequest req,
        ClaimsPrincipal user,
        ITaskService taskService,
        AgentFactory agentFactory,
        BootstrapAgent bootstrapAgent,
        ExpertAgent expertAgent,
        IServiceScopeFactory scopeFactory,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        var task = await taskService.CreateAsync(new CreateTaskRequest(tenantId, req.Goal, req.BudgetUsd, req.Provider), ct);
        var budgetId = task.BudgetId;

        // Fire bootstrap workflow in background — response returns immediately
        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var bootstrap = scope.ServiceProvider.GetRequiredService<BootstrapAgent>();
            var expert = scope.ServiceProvider.GetRequiredService<ExpertAgent>();
            var factory = scope.ServiceProvider.GetRequiredService<AgentFactory>();
            var tasks = scope.ServiceProvider.GetRequiredService<ITaskService>();

            try
            {
                await tasks.UpdateStatusAsync(tenantId, task.Id, "bootstrapping", CancellationToken.None);

                // Step 1: Expert analysis
                var knowledge = await expert.GatherKnowledgeAsync(req.Goal, ct: CancellationToken.None);

                // Step 2: Bootstrap recommendation
                var recommendation = await bootstrap.RecommendTeamAsync(tenantId, task.Id, req.Goal, CancellationToken.None);

                // Step 3: Create executive team
                var team = await factory.CreateFromRecommendationAsync(tenantId, task.Id, budgetId, recommendation, CancellationToken.None);

                // Step 4: Kick off each executive agent (propose KPIs + run strategy)
                var orchestrator = scope.ServiceProvider.GetRequiredService<AgentOrchestrator>();
                await orchestrator.KickoffTeamAsync(tenantId, task.Id, req.Goal, team, CancellationToken.None);

                await tasks.UpdateStatusAsync(tenantId, task.Id, "active", CancellationToken.None);
            }
            catch (Exception ex)
            {
                await tasks.UpdateStatusAsync(tenantId, task.Id, $"failed: {ex.Message}", CancellationToken.None);
            }
        });

        return Results.Accepted($"/api/tasks/{task.Id}", new { task.Id, task.BudgetId, status = "bootstrapping" });
    }

    private static async Task<IResult> ListTasks(
        ClaimsPrincipal user,
        ITaskService taskService,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        var tasks = await taskService.GetAllAsync(tenantId, ct);
        return Results.Ok(tasks);
    }

    private static async Task<IResult> GetTask(
        Guid taskId,
        ClaimsPrincipal user,
        ITaskService taskService,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        var task = await taskService.GetAsync(tenantId, taskId, ct);
        if (task is null) return Results.NotFound();

        var graph = await taskService.GetTeamGraphAsync(tenantId, taskId, ct);
        return Results.Ok(new { task, graph });
    }

    private sealed record SubmitTaskRequest(string Goal, decimal BudgetUsd, string? Provider = null);
}
