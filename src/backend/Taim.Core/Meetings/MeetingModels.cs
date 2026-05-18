namespace Taim.Core.Meetings;

public enum MeetingType { KickoffSync, StatusCheck, DecisionRequest, Escalation, Briefing }

public sealed record MeetingRecord(
    Guid Id, Guid TenantId, Guid? TaskId,
    string Topic, MeetingType MeetingType, string Status,
    Guid? OrganizerAgentId,
    IReadOnlyList<Guid> ParticipantAgentIds,
    string? Summary,
    int MessageCount,
    DateTimeOffset StartedAt, DateTimeOffset? CompletedAt
);

public sealed record MeetingMessageRecord(
    Guid Id, Guid MeetingId, Guid? SpeakerAgentId, string Content, int Sequence, DateTimeOffset CreatedAt
);

public sealed record StartMeetingRequest(
    Guid TenantId, Guid? TaskId,
    MeetingType MeetingType,
    string Topic,
    Guid OrganizerAgentId,
    IReadOnlyList<Guid> ParticipantAgentIds
);

public interface IMeetingStore
{
    Task<MeetingRecord> CreateAsync(StartMeetingRequest request, CancellationToken ct = default);
    Task<MeetingRecord> GetAsync(Guid tenantId, Guid meetingId, CancellationToken ct = default);
    Task<IReadOnlyList<MeetingRecord>> GetForTaskAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);
    Task AddMessageAsync(Guid tenantId, Guid meetingId, Guid speakerAgentId, string content, int sequence, CancellationToken ct = default);
    Task CompleteAsync(Guid tenantId, Guid meetingId, string summary, CancellationToken ct = default);
    Task FailAsync(Guid tenantId, Guid meetingId, CancellationToken ct = default);
    Task<IReadOnlyList<MeetingMessageRecord>> GetMessagesAsync(Guid tenantId, Guid meetingId, CancellationToken ct = default);
}
