using Microsoft.Extensions.AI;

namespace Taim.Core.Memory;

public interface IChatHistoryProvider
{
    Task<IReadOnlyList<ChatMessage>> LoadAsync(
        Guid tenantId, Guid agentId, string sessionId,
        int maxMessages = 50, CancellationToken ct = default);

    Task SaveAsync(
        Guid tenantId, Guid agentId, string sessionId,
        IReadOnlyList<ChatMessage> messages, CancellationToken ct = default);
}
