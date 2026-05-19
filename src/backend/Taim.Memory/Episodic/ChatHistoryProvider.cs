using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using System.Text.Json;
using Taim.Core.Memory;
using Taim.Data;
using Taim.Data.Models;

namespace Taim.Memory.Episodic;

/// <summary>
/// Loads and saves agent chat history from/to PostgreSQL.
/// Used to restore conversation context across agent restarts.
/// </summary>
public sealed class ChatHistoryProvider(TaimDbContext db) : IChatHistoryProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<ChatMessage>> LoadAsync(
        Guid tenantId,
        Guid agentId,
        string sessionId,
        int maxMessages = 50,
        CancellationToken ct = default)
    {
        var rows = await db.AgentChatHistory
            .AsNoTracking()
            .Where(h => h.TenantId == tenantId && h.AgentId == agentId && h.SessionId == sessionId)
            .OrderBy(h => h.Sequence)
            .TakeLast(maxMessages)
            .ToListAsync(ct);

        return rows.Select(ToMessage).ToList();
    }

    public async Task SaveAsync(
        Guid tenantId,
        Guid agentId,
        string sessionId,
        IReadOnlyList<ChatMessage> messages,
        CancellationToken ct = default)
    {
        var existingCount = await db.AgentChatHistory
            .Where(h => h.TenantId == tenantId && h.AgentId == agentId && h.SessionId == sessionId)
            .CountAsync(ct);

        var entities = messages.Select((msg, idx) => new AgentChatHistoryEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AgentId = agentId,
            SessionId = sessionId,
            Role = msg.Role.Value,
            Content = msg.Text ?? string.Empty,
            Sequence = existingCount + idx,
            CreatedAt = DateTimeOffset.UtcNow
        });

        db.AgentChatHistory.AddRange(entities);
        await db.SaveChangesAsync(ct);
    }

    private static ChatMessage ToMessage(AgentChatHistoryEntity row) =>
        new(new ChatRole(row.Role), row.Content);
}
