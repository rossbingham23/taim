using Microsoft.Extensions.AI;

namespace Taim.Core.Meetings;

public interface IMeetingOrchestrator
{
    Task<MeetingRecord> RunAsync(
        StartMeetingRequest request,
        Dictionary<Guid, IChatClient> chatClients,
        CancellationToken ct = default);
}
