using System.Security.Claims;
using Taim.Core.Notifications;
using Taim.Core.System;

namespace Taim.Api.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/system")
                       .RequireAuthorization()
                       .WithTags("System");

        group.MapGet("/status", GetStatus).WithName("GetSystemStatus");
        group.MapPost("/stop", Stop).WithName("StopSystem");
        group.MapPost("/resume", Resume).WithName("ResumeSystem");

        return app;
    }

    private static async Task<IResult> GetStatus(
        ISystemStopService systemStopService,
        CancellationToken ct)
    {
        var stopped = await systemStopService.IsStoppedAsync(ct);
        return Results.Ok(new { stopped });
    }

    private static async Task<IResult> Stop(
        ClaimsPrincipal user,
        ISystemStopService systemStopService,
        INotificationService notificationService,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        await systemStopService.StopAsync(ct);
        await notificationService.NotifyAsync(tenantId, NotificationKind.SystemStopped,
            "System stopped", "All agent activity halted.", null, ct);

        return Results.NoContent();
    }

    private static async Task<IResult> Resume(
        ClaimsPrincipal user,
        ISystemStopService systemStopService,
        INotificationService notificationService,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId))
            return Results.Unauthorized();

        await systemStopService.ResumeAsync(ct);
        await notificationService.NotifyAsync(tenantId, NotificationKind.SystemResumed,
            "System resumed", "Agent activity resumed.", null, ct);

        return Results.NoContent();
    }
}
