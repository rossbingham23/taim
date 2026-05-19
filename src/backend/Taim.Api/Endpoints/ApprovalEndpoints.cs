using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Taim.Core.Approvals;

namespace Taim.Api.Endpoints;

public static class ApprovalEndpoints
{
    public static IEndpointRouteBuilder MapApprovalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/approvals")
                       .RequireAuthorization()
                       .WithTags("Approvals");

        group.MapGet("/", GetPending).WithName("GetPendingApprovals");
        group.MapGet("/history", GetHistory).WithName("GetApprovalHistory");
        group.MapPost("/{approvalId:guid}/decide", Decide).WithName("DecideApproval");

        return app;
    }

    private static async Task<IResult> GetPending(
        ClaimsPrincipal user,
        IApprovalService approvalService,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        var pending = await approvalService.GetPendingAsync(tenantId, ct);
        return Results.Ok(pending);
    }

    private static async Task<IResult> GetHistory(
        [Microsoft.AspNetCore.Mvc.FromQuery] string? taskId,
        ClaimsPrincipal user,
        IApprovalService approvalService,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        if (string.IsNullOrEmpty(taskId) || !Guid.TryParse(taskId, out var taskGuid))
            return Results.BadRequest(new { error = "taskId query parameter is required" });

        var history = await approvalService.GetHistoryAsync(tenantId, taskGuid, ct);
        return Results.Ok(history);
    }

    private static async Task<IResult> Decide(
        Guid approvalId,
        [FromBody] DecideRequest req,
        ClaimsPrincipal user,
        IApprovalService approvalService,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        if (!Enum.TryParse<ApprovalScope>(req.Scope, ignoreCase: true, out var scope))
            scope = ApprovalScope.Once;

        var pending = await approvalService.GetPendingAsync(tenantId, ct);
        if (!pending.Any(a => a.Id == approvalId))
            return Results.NotFound();

        var decision = new ApprovalDecision(approvalId, req.Approved, scope, req.ScopeKey);
        await approvalService.ApplyDecisionAsync(tenantId, decision, ct);

        return Results.NoContent();
    }

    private sealed record DecideRequest(bool Approved, string Scope = "Once", string? ScopeKey = null);
}
