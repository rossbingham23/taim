using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Taim.Core.KPIs;

namespace Taim.Api.Endpoints;

public static class KpiEndpoints
{
    public static IEndpointRouteBuilder MapKpiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/kpis")
                       .RequireAuthorization()
                       .WithTags("KPIs");

        group.MapGet("/", ListRootKpis).WithName("ListKpis");
        group.MapGet("/{kpiId:guid}", GetKpiHierarchy).WithName("GetKpiHierarchy");
        group.MapGet("/{kpiId:guid}/values", GetKpiValues).WithName("GetKpiValues");
        group.MapPost("/{kpiId:guid}/values", RecordKpiValue).WithName("RecordKpiValue");

        return app;
    }

    private static async Task<IResult> ListRootKpis(
        ClaimsPrincipal user,
        IKpiService kpiService,
        Guid? taskId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        if (taskId.HasValue)
        {
            var roots = await kpiService.GetTaskHierarchyRootsAsync(tenantId, taskId.Value, ct);
            return Results.Ok(roots);
        }

        return Results.BadRequest(new { error = "taskId query parameter is required" });
    }

    private static async Task<IResult> GetKpiHierarchy(
        Guid kpiId,
        ClaimsPrincipal user,
        IKpiService kpiService,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        try
        {
            var hierarchy = await kpiService.GetHierarchyAsync(tenantId, kpiId, ct);
            return Results.Ok(hierarchy);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> GetKpiValues(
        Guid kpiId,
        ClaimsPrincipal user,
        IKpiService kpiService,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        try
        {
            var hierarchy = await kpiService.GetHierarchyAsync(tenantId, kpiId, ct);
            return Results.Ok(new { kpi = hierarchy.Node, latestValue = hierarchy.LatestValue, children = hierarchy.Children });
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> RecordKpiValue(
        Guid kpiId,
        [FromBody] RecordValueRequest req,
        ClaimsPrincipal user,
        IKpiService kpiService,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        try
        {
            await kpiService.RecordValueAsync(tenantId, new RecordKpiValueRequest(kpiId, req.Value, req.Source), ct);
            return Results.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private sealed record RecordValueRequest(string Value, string? Source = null);
}
