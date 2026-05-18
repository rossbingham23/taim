namespace Taim.Core.Notifications;

public enum NotificationKind
{
    ApprovalRequired,
    AgentStatusChanged,
    ExecutiveReport,
    BudgetAlert,
    TeamUpdate,
    MeetingStarted,
    MeetingMessage,
    MeetingCompleted,
    AgentLog,
    ActionCreated,
    ActionUpdated
}

public sealed record Notification(
    Guid Id,
    Guid TenantId,
    NotificationKind Kind,
    string Title,
    string Body,
    Dictionary<string, object?> Metadata,
    DateTimeOffset CreatedAt
);

public interface INotificationChannel
{
    string ChannelId { get; }
    Task SendAsync(Notification notification, CancellationToken ct = default);
}

public interface INotificationService
{
    Task NotifyAsync(Guid tenantId, NotificationKind kind, string title, string body,
        Dictionary<string, object?>? metadata = null, CancellationToken ct = default);
}
