using Microsoft.EntityFrameworkCore;
using Taim.Core.Agents;
using Taim.Data.Models;

namespace Taim.Data.Services;

public sealed class AgentRegistryService(TaimDbContext db) : IAgentRegistry
{
    public async Task<AgentDefinition> RegisterAsync(CreateAgentRequest request, CancellationToken ct = default)
    {
        var entity = new AgentEntity
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            TaskId = request.TaskId,
            ParentAgentId = request.ParentAgentId,
            Name = request.Name,
            Role = request.Role.ToString().ToLowerInvariant(),
            Charter = request.Charter,
            Status = "idle",
            Provider = request.Provider,
            Model = request.Model,
            DurableEntityKey = $"{request.TenantId}/{Guid.NewGuid()}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Agents.Add(entity);
        await db.SaveChangesAsync(ct);
        return MapToDefinition(entity);
    }

    public async Task<AgentDefinition?> GetAsync(Guid tenantId, Guid agentId, CancellationToken ct = default)
    {
        var entity = await db.Agents.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == agentId && a.TenantId == tenantId, ct);
        return entity is null ? null : MapToDefinition(entity);
    }

    public async Task<IReadOnlyList<AgentDefinition>> GetTeamAsync(Guid tenantId, Guid? parentAgentId, CancellationToken ct = default)
    {
        var query = db.Agents.AsNoTracking().Where(a => a.TenantId == tenantId);
        query = parentAgentId is null
            ? query.Where(a => a.ParentAgentId == null)
            : query.Where(a => a.ParentAgentId == parentAgentId);

        var entities = await query.OrderBy(a => a.CreatedAt).ToListAsync(ct);
        return entities.Select(MapToDefinition).ToList();
    }

    public async Task UpdateStatusAsync(Guid tenantId, Guid agentId, AgentStatus status, CancellationToken ct = default)
    {
        var entity = await db.Agents.FirstOrDefaultAsync(a => a.Id == agentId && a.TenantId == tenantId, ct);
        if (entity is null) return;
        entity.Status = status.ToString().ToLowerInvariant();
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetDurableEntityKeyAsync(Guid tenantId, Guid agentId, string entityKey, CancellationToken ct = default)
    {
        var entity = await db.Agents.FirstOrDefaultAsync(a => a.Id == agentId && a.TenantId == tenantId, ct);
        if (entity is null) return;
        entity.DurableEntityKey = entityKey;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static AgentDefinition MapToDefinition(AgentEntity e) => new(
        e.Id,
        e.TenantId,
        e.TaskId,
        e.ParentAgentId,
        e.Name,
        Enum.TryParse<AgentRole>(e.Role, ignoreCase: true, out var role) ? role : AgentRole.Generic,
        e.Charter ?? string.Empty,
        Enum.TryParse<AgentStatus>(e.Status, ignoreCase: true, out var status) ? status : AgentStatus.Idle,
        e.Provider,
        e.Model,
        e.DurableEntityKey,
        e.CreatedAt,
        e.UpdatedAt
    );
}
