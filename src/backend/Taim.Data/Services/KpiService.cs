using Microsoft.EntityFrameworkCore;
using Taim.Core.KPIs;
using Taim.Data.Models;

namespace Taim.Data.Services;

public sealed class KpiService(TaimDbContext db) : IKpiService
{
    public async Task<KpiNode> CreateAsync(CreateKpiRequest request, CancellationToken ct = default)
    {
        var entity = new KpiEntity
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            AgentId = request.AgentId,
            ParentKpiId = request.ParentKpiId,
            Name = request.Name,
            Description = request.Description,
            TargetValue = request.TargetValue,
            Unit = request.Unit,
            Direction = request.Direction.ToString(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Kpis.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToNode(entity);
    }

    public async Task RecordValueAsync(Guid tenantId, RecordKpiValueRequest request, CancellationToken ct = default)
    {
        var exists = await db.Kpis.AnyAsync(k => k.TenantId == tenantId && k.Id == request.KpiId, ct);
        if (!exists) throw new KeyNotFoundException($"KPI {request.KpiId} not found.");

        var entity = new KpiValueEntity
        {
            Id = Guid.NewGuid(),
            KpiId = request.KpiId,
            Value = request.Value,
            Source = request.Source,
            RecordedAt = DateTimeOffset.UtcNow
        };
        db.KpiValues.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<KpiHierarchy> GetHierarchyAsync(Guid tenantId, Guid rootKpiId, CancellationToken ct = default)
    {
        var allKpis = await db.Kpis
            .AsNoTracking()
            .Where(k => k.TenantId == tenantId)
            .ToListAsync(ct);

        var latestValues = await db.KpiValues
            .AsNoTracking()
            .Where(v => allKpis.Select(k => k.Id).Contains(v.KpiId))
            .GroupBy(v => v.KpiId)
            .Select(g => g.OrderByDescending(v => v.RecordedAt).First())
            .ToListAsync(ct);

        var valueMap = latestValues.ToDictionary(v => v.KpiId);
        var nodeMap = allKpis.ToDictionary(k => k.Id, k => ToNode(k));

        return BuildHierarchy(rootKpiId, nodeMap, valueMap, allKpis);
    }

    public async Task<IReadOnlyList<KpiNode>> GetForAgentAsync(Guid tenantId, Guid agentId, CancellationToken ct = default)
    {
        var entities = await db.Kpis
            .AsNoTracking()
            .Where(k => k.TenantId == tenantId && k.AgentId == agentId)
            .OrderBy(k => k.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(ToNode).ToList();
    }

    public async Task<IReadOnlyList<KpiNode>> GetTaskHierarchyRootsAsync(Guid tenantId, Guid taskId, CancellationToken ct = default)
    {
        // Root KPIs are those with no parent, belonging to agents in this task
        var taskAgentIds = await db.Agents
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.TaskId == taskId)
            .Select(a => a.Id)
            .ToListAsync(ct);

        var roots = await db.Kpis
            .AsNoTracking()
            .Where(k => k.TenantId == tenantId && k.ParentKpiId == null && taskAgentIds.Contains(k.AgentId))
            .ToListAsync(ct);

        return roots.Select(ToNode).ToList();
    }

    private static KpiHierarchy BuildHierarchy(
        Guid nodeId,
        Dictionary<Guid, KpiNode> nodeMap,
        Dictionary<Guid, KpiValueEntity> valueMap,
        IReadOnlyList<KpiEntity> allEntities)
    {
        var node = nodeMap[nodeId];
        var latestValue = valueMap.TryGetValue(nodeId, out var val)
            ? new KpiValue(val.Id, val.KpiId, val.Value, val.RecordedAt, val.Source)
            : null;

        var children = allEntities
            .Where(k => k.ParentKpiId == nodeId)
            .Select(k => BuildHierarchy(k.Id, nodeMap, valueMap, allEntities))
            .ToList();

        return new KpiHierarchy(node, children, latestValue);
    }

    private static KpiNode ToNode(KpiEntity e) => new(
        e.Id, e.TenantId, e.AgentId,
        e.ParentKpiId, e.Name, e.Description,
        e.TargetValue, e.Unit,
        Enum.TryParse<KpiDirection>(e.Direction, out var dir) ? dir : KpiDirection.HigherIsBetter,
        e.CreatedAt);
}
