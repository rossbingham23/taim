using Microsoft.EntityFrameworkCore;
using Taim.Core.Meetings;
using Taim.Data.Models;

namespace Taim.Data.Services;

public sealed class MeetingService(TaimDbContext db) : IMeetingStore
{
    public async Task<MeetingRecord> CreateAsync(StartMeetingRequest request, CancellationToken ct = default)
    {
        var entity = new MeetingEntity
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            TaskId = request.TaskId,
            Topic = request.Topic,
            MeetingType = MeetingTypeToString(request.MeetingType),
            Status = "in_progress",
            OrganizerAgentId = request.OrganizerAgentId,
            StartedAt = DateTimeOffset.UtcNow,
            Participants = request.ParticipantAgentIds
                .Select(agentId => new MeetingParticipantEntity { AgentId = agentId, Role = "participant" })
                .Append(new MeetingParticipantEntity { AgentId = request.OrganizerAgentId, Role = "organizer" })
                .ToList()
        };

        foreach (var p in entity.Participants)
            p.MeetingId = entity.Id;

        db.Meetings.Add(entity);
        await db.SaveChangesAsync(ct);

        return ToRecord(entity, 0);
    }

    public async Task<MeetingRecord> GetAsync(Guid tenantId, Guid meetingId, CancellationToken ct = default)
    {
        var entity = await db.Meetings
            .AsNoTracking()
            .Include(m => m.Participants)
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == meetingId, ct)
            ?? throw new KeyNotFoundException($"Meeting {meetingId} not found.");

        var count = await db.MeetingMessages.CountAsync(m => m.MeetingId == meetingId, ct);
        return ToRecord(entity, count);
    }

    public async Task<IReadOnlyList<MeetingRecord>> GetForTaskAsync(Guid tenantId, Guid taskId, CancellationToken ct = default)
    {
        var entities = await db.Meetings
            .AsNoTracking()
            .Include(m => m.Participants)
            .Where(m => m.TenantId == tenantId && m.TaskId == taskId)
            .OrderByDescending(m => m.StartedAt)
            .ToListAsync(ct);

        var meetingIds = entities.Select(e => e.Id).ToList();
        var counts = await db.MeetingMessages
            .Where(m => meetingIds.Contains(m.MeetingId))
            .GroupBy(m => m.MeetingId)
            .Select(g => new { MeetingId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.MeetingId, g => g.Count, ct);

        return entities.Select(e => ToRecord(e, counts.GetValueOrDefault(e.Id))).ToList();
    }

    public async Task AddMessageAsync(
        Guid tenantId, Guid meetingId, Guid speakerAgentId,
        string content, int sequence, CancellationToken ct = default)
    {
        var entity = new MeetingMessageEntity
        {
            Id = Guid.NewGuid(),
            MeetingId = meetingId,
            TenantId = tenantId,
            AgentId = speakerAgentId,
            Role = "assistant",
            Content = content,
            Sequence = sequence,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.MeetingMessages.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task CompleteAsync(Guid tenantId, Guid meetingId, string summary, CancellationToken ct = default)
    {
        var entity = await db.Meetings.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == meetingId, ct);
        if (entity is null) return;

        entity.Status = "completed";
        entity.EndedAt = DateTimeOffset.UtcNow;
        entity.Summary = summary;
        await db.SaveChangesAsync(ct);
    }

    public async Task FailAsync(Guid tenantId, Guid meetingId, CancellationToken ct = default)
    {
        var entity = await db.Meetings.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == meetingId, ct);
        if (entity is null) return;

        entity.Status = "failed";
        entity.EndedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MeetingMessageRecord>> GetMessagesAsync(Guid tenantId, Guid meetingId, CancellationToken ct = default)
    {
        var entities = await db.MeetingMessages
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.MeetingId == meetingId)
            .OrderBy(m => m.Sequence)
            .ToListAsync(ct);

        return entities
            .Select(e => new MeetingMessageRecord(e.Id, e.MeetingId, e.AgentId, e.Content, e.Sequence, e.CreatedAt))
            .ToList();
    }

    private static MeetingRecord ToRecord(MeetingEntity e, int messageCount)
    {
        var participantIds = e.Participants
            .Where(p => p.Role == "participant")
            .Select(p => p.AgentId)
            .ToList();

        return new MeetingRecord(
            e.Id, e.TenantId, e.TaskId,
            e.Topic, StringToMeetingType(e.MeetingType), e.Status,
            e.OrganizerAgentId, participantIds,
            e.Summary, messageCount,
            e.StartedAt, e.EndedAt);
    }

    private static string MeetingTypeToString(MeetingType t) => t switch
    {
        MeetingType.KickoffSync      => "kickoff_sync",
        MeetingType.StatusCheck      => "status_check",
        MeetingType.DecisionRequest  => "decision_request",
        MeetingType.Escalation       => "escalation",
        MeetingType.Briefing         => "briefing",
        _                            => "kickoff_sync",
    };

    private static MeetingType StringToMeetingType(string s) => s switch
    {
        "kickoff_sync"     => MeetingType.KickoffSync,
        "status_check"     => MeetingType.StatusCheck,
        "decision_request" => MeetingType.DecisionRequest,
        "escalation"       => MeetingType.Escalation,
        "briefing"         => MeetingType.Briefing,
        _                  => MeetingType.KickoffSync,
    };
}
