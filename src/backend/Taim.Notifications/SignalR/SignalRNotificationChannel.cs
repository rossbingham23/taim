using Microsoft.AspNetCore.SignalR.Client;
using Taim.Core.Notifications;

namespace Taim.Notifications.SignalR;

public sealed class SignalRNotificationChannel : INotificationChannel, IAsyncDisposable
{
    private readonly HubConnection _connection;

    public string ChannelId => "signalr";

    public SignalRNotificationChannel(string hubUrl)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();
    }

    public async Task StartAsync(CancellationToken ct = default) =>
        await _connection.StartAsync(ct);

    public async Task SendAsync(Notification notification, CancellationToken ct = default) =>
        await _connection.InvokeAsync("SendNotification", notification.TenantId, notification, ct);

    public async ValueTask DisposeAsync() =>
        await _connection.DisposeAsync();
}
