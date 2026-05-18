using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Taim.Core.Actions;

namespace Taim.Api.Endpoints;

public static class ActionEndpoints
{
    public static IEndpointRouteBuilder MapActionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/actions")
                       .RequireAuthorization()
                       .WithTags("Actions");

        group.MapGet("/", ListActions).WithName("ListActions");
        group.MapPost("/", CreateAction).WithName("CreateAction");
        group.MapPatch("/{actionId:guid}", UpdateAction).WithName("UpdateAction");

        return app;
    }

    private static async Task<IResult> ListActions(
        ClaimsPrincipal user,
        IActionService actionService,
        Guid? taskId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        if (!taskId.HasValue)
            return Results.BadRequest(new { error = "taskId query parameter is required" });

        var actions = await actionService.GetForTaskAsync(tenantId, taskId.Value, ct);
        return Results.Ok(actions);
    }

    private static async Task<IResult> CreateAction(
        [FromBody] CreateActionBody req,
        ClaimsPrincipal user,
        IActionService actionService,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        var action = await actionService.CreateAsync(new CreateActionRequest(
            tenantId, req.TaskId, req.AgentId, null,
            req.Title, req.Description, req.Priority ?? 50, req.ParentActionId, req.DueAt), ct);

        return Results.Created($"/api/actions/{action.Id}", action);
    }

    private static async Task<IResult> UpdateAction(
        Guid actionId,
        [FromBody] UpdateActionRequest req,
        ClaimsPrincipal user,
        IActionService actionService,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        try
        {
            var updated = await actionService.UpdateAsync(tenantId, actionId, req, ct);
            return Results.Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private sealed record CreateActionBody(
        Guid TaskId,
        string Title,
        string? Description = null,
        Guid? AgentId = null,
        int? Priority = null,
        Guid? ParentActionId = null,
        DateTimeOffset? DueAt = null);
}
