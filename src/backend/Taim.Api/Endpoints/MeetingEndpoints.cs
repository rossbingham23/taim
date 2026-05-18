using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Taim.Core.Meetings;

namespace Taim.Api.Endpoints;

public static class MeetingEndpoints
{
    public static IEndpointRouteBuilder MapMeetingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/meetings")
                       .RequireAuthorization()
                       .WithTags("Meetings");

        group.MapGet("/", ListMeetings).WithName("ListMeetings");
        group.MapGet("/{meetingId:guid}", GetMeeting).WithName("GetMeeting");

        return app;
    }

    private static async Task<IResult> ListMeetings(
        ClaimsPrincipal user,
        IMeetingStore store,
        Guid? taskId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        if (!taskId.HasValue)
            return Results.BadRequest(new { error = "taskId query parameter is required" });

        var meetings = await store.GetForTaskAsync(tenantId, taskId.Value, ct);
        return Results.Ok(meetings);
    }

    private static async Task<IResult> GetMeeting(
        Guid meetingId,
        ClaimsPrincipal user,
        IMeetingStore store,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        try
        {
            var meeting  = await store.GetAsync(tenantId, meetingId, ct);
            var messages = await store.GetMessagesAsync(tenantId, meetingId, ct);
            return Results.Ok(new { meeting, messages });
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    }
}
