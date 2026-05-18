using System.Security.Claims;
using Taim.Core.Activity;

namespace Taim.Api.Endpoints;

public static class ActivityEndpoints
{
    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/activity")
                       .RequireAuthorization()
                       .WithTags("Activity");

        group.MapGet("/", ListActivity).WithName("ListActivity");

        return app;
    }

    private static IResult ListActivity(
        ClaimsPrincipal user,
        IActivityFeed feed,
        string? taskId,
        int limit = 200)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out _))
            return Results.Unauthorized();

        var capped = Math.Min(limit, 500);
        var entries = feed.GetRecent(capped, taskId);
        return Results.Ok(entries);
    }
}
