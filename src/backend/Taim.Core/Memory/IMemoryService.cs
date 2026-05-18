namespace Taim.Core.Memory;

public sealed record MemoryEntry(
    Guid Id,
    Guid TenantId,
    Guid? AgentId,
    string Collection,
    string Content,
    Dictionary<string, object?> Metadata,
    DateTimeOffset CreatedAt
);

public sealed record MemorySearchResult(MemoryEntry Entry, double Score);

public interface IMemoryService
{
    Task<MemoryEntry> StoreAsync(Guid tenantId, Guid? agentId, string collection, string content,
        Dictionary<string, object?>? metadata = null, CancellationToken ct = default);

    Task<IReadOnlyList<MemorySearchResult>> SearchAsync(Guid tenantId, string query,
        string? collection = null, Guid? agentId = null, int limit = 5, CancellationToken ct = default);

    Task DeleteAsync(Guid tenantId, Guid entryId, CancellationToken ct = default);
}
