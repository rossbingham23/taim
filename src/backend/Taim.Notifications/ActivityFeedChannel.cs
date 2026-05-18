using System.Collections.Concurrent;
using Taim.Core.Activity;
using Taim.Core.Notifications;

namespace Taim.Notifications;

public sealed class ActivityFeedChannel : INotificationChannel, IActivityFeed
{
    private const int MaxEntries = 2000;

    private readonly ConcurrentQueue<Notification> _queue = new();

    public string ChannelId => "activity-feed";

    public Task SendAsync(Notification notification, CancellationToken ct = default)
    {
        Append(notification);
        return Task.CompletedTask;
    }

    public void Append(Notification notification)
    {
        _queue.Enqueue(notification);

        // Trim to MaxEntries — dequeue oldest when over limit
        while (_queue.Count > MaxEntries)
            _queue.TryDequeue(out _);
    }

    public IReadOnlyList<Notification> GetRecent(int max = 200, string? taskId = null)
    {
        var entries = _queue.ToArray(); // snapshot

        if (taskId is not null)
            entries = entries
                .Where(n => n.Metadata.TryGetValue("taskId", out var t) && t?.ToString() == taskId)
                .ToArray();

        return entries
            .OrderByDescending(n => n.CreatedAt)
            .Take(max)
            .OrderBy(n => n.CreatedAt)
            .ToArray();
    }
}
