# Taim.Notifications — Notification Broadcasting

Implements `INotificationService` which fans out to all `INotificationChannel` implementations.

## Key Files

| File | Purpose |
|---|---|
| `NotificationService.cs` | Singleton — iterates all `INotificationChannel` registrations and calls `SendAsync` |
| `SignalR/SignalRNotificationChannel.cs` | Sends to SignalR group `tenant:{tenantId}` via `IHubContext<AgentEventHub>` |
| `ActivityFeedChannel.cs` | Captures events in a `ConcurrentQueue<Notification>` for the `/api/activity` feed |

## Registration (Program.cs)

```csharp
builder.Services.AddSingleton<INotificationChannel, AgentHubNotifier>();
builder.Services.AddSingleton<ActivityFeedChannel>();
builder.Services.AddSingleton<IActivityFeed>(sp => sp.GetRequiredService<ActivityFeedChannel>());
builder.Services.AddSingleton<INotificationChannel>(sp => sp.GetRequiredService<ActivityFeedChannel>());
builder.Services.AddSingleton<INotificationService, NotificationService>();
```

`NotificationService` resolves `IEnumerable<INotificationChannel>` — adding a new channel is as simple as registering another `INotificationChannel` singleton.

## Channel Contracts

```csharp
interface INotificationChannel {
    string ChannelId { get; }
    Task SendAsync(Notification notification, CancellationToken ct = default);
}
```

## Activity Feed

`ActivityFeedChannel` also implements `IActivityFeed`:
```csharp
interface IActivityFeed {
    IEnumerable<Notification> GetRecent(string? taskId, int limit);
}
```

`GET /api/activity?taskId=&limit=` reads from this feed — it is in-memory only and resets on restart.
