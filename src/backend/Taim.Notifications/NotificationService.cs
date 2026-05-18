using Taim.Core.Notifications;

namespace Taim.Notifications;

public sealed class NotificationService(IEnumerable<INotificationChannel> channels) : INotificationService
{
    public async Task NotifyAsync(Guid tenantId, NotificationKind kind, string title, string body,
        Dictionary<string, object?>? metadata = null, CancellationToken ct = default)
    {
        var notification = new Notification(
            Guid.NewGuid(), tenantId, kind, title, body,
            metadata ?? [], DateTimeOffset.UtcNow);

        var tasks = channels.Select(ch => ch.SendAsync(notification, ct));
        await Task.WhenAll(tasks);
    }
}
