using Taim.Core.Notifications;

namespace Taim.Core.Activity;

public interface IActivityFeed
{
    void Append(Notification notification);
    IReadOnlyList<Notification> GetRecent(int max = 200, string? taskId = null);
}
