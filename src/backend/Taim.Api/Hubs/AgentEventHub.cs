using Microsoft.AspNetCore.SignalR;
using Taim.Core.Notifications;

namespace Taim.Api.Hubs;

/// <summary>
/// Real-time hub for pushing agent events, approval requests, and status updates to the frontend.
/// Clients join a tenant-scoped group on connect.
/// </summary>
public sealed class AgentEventHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Tenant extracted from JWT claim; client joins their isolated group
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
        if (tenantId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}");
        await base.OnConnectedAsync();
    }

    // Called by NotificationService to broadcast to a tenant's connected clients
    public Task SendNotification(string tenantId, Notification notification) =>
        Clients.Group($"tenant:{tenantId}").SendAsync("notification", notification);
}

/// <summary>
/// Helper to push notifications from background services via IHubContext.
/// </summary>
public sealed class AgentHubNotifier(IHubContext<AgentEventHub> hub) : INotificationChannel
{
    public string ChannelId => "signalr-hub";

    public Task SendAsync(Notification notification, CancellationToken ct = default) =>
        hub.Clients
           .Group($"tenant:{notification.TenantId}")
           .SendAsync("notification", notification, ct);
}
