using Microsoft.EntityFrameworkCore;
using Taim.Core.Budget;
using Taim.Core.Teams;
using Taim.Data.Models;

namespace Taim.Data.Services;

public sealed class TaskService(TaimDbContext db, IBudgetService budgetService) : ITaskService
{
    public async Task<TaskRecord> CreateAsync(CreateTaskRequest request, CancellationToken ct = default)
    {
        // Create a budget for the task first
        var budget = await budgetService.CreateAsync(request.TenantId, null, request.BudgetLimitUsd, ct);

        var entity = new TaskEntity
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Goal = request.Goal,
            Status = "pending",
            BudgetId = budget.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Tasks.Add(entity);
        await db.SaveChangesAsync(ct);

        // Back-fill budget.task_id now that we have the task ID
        var budgetEntity = await db.Budgets.FirstAsync(b => b.Id == budget.Id, ct);
        budgetEntity.TaskId = entity.Id;
        await db.SaveChangesAsync(ct);

        return MapToRecord(entity);
    }

    public async Task<TaskRecord?> GetAsync(Guid tenantId, Guid taskId, CancellationToken ct = default)
    {
        var entity = await db.Tasks.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == taskId && t.TenantId == tenantId, ct);
        return entity is null ? null : MapToRecord(entity);
    }

    public async Task<IReadOnlyList<TaskRecord>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        var entities = await db.Tasks.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
        return entities.Select(MapToRecord).ToList();
    }

    public async Task UpdateStatusAsync(Guid tenantId, Guid taskId, string status, CancellationToken ct = default)
    {
        var entity = await db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.TenantId == tenantId, ct);
        if (entity is null) return;
        entity.Status = status;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<TeamGraph> GetTeamGraphAsync(Guid tenantId, Guid taskId, CancellationToken ct = default)
    {
        var agents = await db.Agents.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.TaskId == taskId)
            .ToListAsync(ct);

        var kpisByAgent = await db.Kpis.AsNoTracking()
            .Where(k => k.TenantId == tenantId && agents.Select(a => a.Id).Contains(k.AgentId))
            .GroupBy(k => k.AgentId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(k => k.Id).ToList(), ct);

        var nodes = agents.Select(a => new TeamNode(
            a.Id,
            a.Name,
            a.Role,
            a.Status,
            ComputeDepth(a.Id, agents),
            (kpisByAgent.GetValueOrDefault(a.Id) ?? []).AsReadOnly()
        )).ToList();

        var edges = agents
            .Where(a => a.ParentAgentId.HasValue)
            .Select(a => new TeamEdge(a.ParentAgentId!.Value, a.Id))
            .ToList();

        return new TeamGraph(taskId, nodes, edges);
    }

    private static int ComputeDepth(Guid agentId, List<AgentEntity> allAgents)
    {
        var agent = allAgents.FirstOrDefault(a => a.Id == agentId);
        if (agent?.ParentAgentId is null) return 0;
        return 1 + ComputeDepth(agent.ParentAgentId.Value, allAgents);
    }

    private static TaskRecord MapToRecord(TaskEntity e) => new(
        e.Id, e.TenantId, e.Goal, e.Status, e.BudgetId, e.CreatedAt, e.UpdatedAt
    );
}
