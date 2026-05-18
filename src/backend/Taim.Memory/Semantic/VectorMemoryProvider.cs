using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Taim.Core.Memory;
using Taim.Data;
using Taim.Data.Models;

namespace Taim.Memory.Semantic;

/// <summary>
/// Stores and retrieves memories using pgvector cosine similarity search.
/// Implements IMemoryService; used by agents for RAG over past context.
/// </summary>
public sealed class VectorMemoryProvider(TaimDbContext db, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator) : IMemoryService
{
    public async Task<MemoryEntry> StoreAsync(
        Guid tenantId,
        Guid? agentId,
        string collection,
        string content,
        Dictionary<string, object?>? metadata = null,
        CancellationToken ct = default)
    {
        var embeddings = await embeddingGenerator.GenerateAsync([content], cancellationToken: ct);
        var vector = new Vector(embeddings[0].Vector.ToArray());

        var entity = new MemoryEntryEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AgentId = agentId,
            Collection = collection,
            Content = content,
            Embedding = vector,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.MemoryEntries.Add(entity);
        await db.SaveChangesAsync(ct);

        return ToRecord(entity, metadata ?? new Dictionary<string, object?>());
    }

    public async Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
        Guid tenantId,
        string query,
        string? collection = null,
        Guid? agentId = null,
        int limit = 5,
        CancellationToken ct = default)
    {
        var embeddings = await embeddingGenerator.GenerateAsync([query], cancellationToken: ct);
        var queryVector = new Vector(embeddings[0].Vector.ToArray());

        var q = db.MemoryEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.Embedding != null);

        if (collection is not null)
            q = q.Where(e => e.Collection == collection);

        if (agentId.HasValue)
            q = q.Where(e => e.AgentId == agentId);

        // pgvector cosine distance (<=> operator) — lower is more similar
        var results = await q
            .OrderBy(e => e.Embedding!.CosineDistance(queryVector))
            .Take(limit)
            .ToListAsync(ct);

        // Return results in descending relevance; actual distance not available client-side
        // so we assign decreasing scores based on rank position
        return results.Select((e, idx) => new MemorySearchResult(
            ToRecord(e, new Dictionary<string, object?>()),
            Math.Max(0.0, 1.0 - (idx * 0.1))
        )).ToList();
    }

    public async Task DeleteAsync(Guid tenantId, Guid entryId, CancellationToken ct = default)
    {
        var entry = await db.MemoryEntries.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == entryId, ct);
        if (entry is not null)
        {
            db.MemoryEntries.Remove(entry);
            await db.SaveChangesAsync(ct);
        }
    }

    private static MemoryEntry ToRecord(MemoryEntryEntity e, Dictionary<string, object?> metadata) =>
        new(e.Id, e.TenantId, e.AgentId, e.Collection, e.Content, metadata, e.CreatedAt);
}
