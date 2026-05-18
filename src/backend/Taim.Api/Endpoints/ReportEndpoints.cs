using System.Security.Claims;
using Taim.Core.Reports;

namespace Taim.Api.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports")
                       .RequireAuthorization()
                       .WithTags("Reports");

        group.MapGet("/", ListReports).WithName("ListReports");

        return app;
    }

    private static async Task<IResult> ListReports(
        ClaimsPrincipal user,
        IReportService reportService,
        Guid? taskId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        if (!taskId.HasValue)
            return Results.BadRequest(new { error = "taskId query parameter is required" });

        var reports = await reportService.GetTaskReportsAsync(tenantId, taskId.Value, ct);

        return Results.Ok(reports.Select(r => new
        {
            id = r.Id,
            agentId = r.AgentId,
            agentName = r.AgentName,
            title = r.Title,
            content = r.Content,
            generatedAt = r.GeneratedAt,
        }));
    }
}
