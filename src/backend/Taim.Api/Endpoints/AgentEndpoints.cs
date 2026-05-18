using System.Security.Claims;
using Taim.Core.Agents;
using Taim.Core.KPIs;

namespace Taim.Api.Endpoints;

public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/agents")
                       .RequireAuthorization()
                       .WithTags("Agents");

        group.MapGet("/", ListAgents).WithName("ListAgents");
        group.MapGet("/{agentId:guid}", GetAgent).WithName("GetAgent");

        return app;
    }

    private static async Task<IResult> ListAgents(
        ClaimsPrincipal user,
        IAgentRegistry registry,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        var agents = await registry.GetTeamAsync(tenantId, parentAgentId: null, ct);
        return Results.Ok(agents);
    }

    private static async Task<IResult> GetAgent(
        Guid agentId,
        ClaimsPrincipal user,
        IAgentRegistry registry,
        IKpiService kpiService,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        var agent = await registry.GetAsync(tenantId, agentId, ct);
        if (agent is null) return Results.NotFound();

        var kpis = await kpiService.GetForAgentAsync(tenantId, agentId, ct);
        var reports = await registry.GetTeamAsync(tenantId, agentId, ct);

        return Results.Ok(new { agent, kpis, directReports = reports });
    }
}
