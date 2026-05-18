using Microsoft.EntityFrameworkCore;
using Taim.Core.Actions;
using Taim.Data.Models;

namespace Taim.Data.Services;

public sealed class ActionService(TaimDbContext db) : IActionService
{
    public async Task<ActionRecord> CreateAsync(CreateActionRequest request, CancellationToken ct = default)
    {
        var entity = new ActionEntity
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            TaskId = request.TaskId,
            AgentId = request.AgentId,
            CreatedByAgentId = request.CreatedByAgentId,
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            ParentActionId = request.ParentActionId,
            DueAt = request.DueAt,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        db.Actions.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToRecord(entity);
    }

    public async Task<IReadOnlyList<ActionRecord>> GetForTaskAsync(Guid tenantId, Guid taskId, CancellationToken ct = default)
    {
        var rows = await db.Actions
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.TaskId == taskId)
            .OrderBy(a => a.Priority)
            .ThenBy(a => a.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(ToRecord).ToList();
    }

    public async Task<ActionRecord?> GetAsync(Guid tenantId, Guid actionId, CancellationToken ct = default)
    {
        var entity = await db.Actions
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == actionId, ct);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task<ActionRecord> UpdateAsync(Guid tenantId, Guid actionId, UpdateActionRequest request, CancellationToken ct = default)
    {
        var entity = await db.Actions
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == actionId, ct)
            ?? throw new KeyNotFoundException($"Action {actionId} not found.");

        if (request.Status is not null)
        {
            entity.Status = request.Status;
            if (request.Status == "done" && entity.CompletedAt is null)
                entity.CompletedAt = DateTimeOffset.UtcNow;
        }
        if (request.Title is not null) entity.Title = request.Title;
        if (request.Description is not null) entity.Description = request.Description;
        if (request.Priority is not null) entity.Priority = request.Priority.Value;
        if (request.AgentId is not null) entity.AgentId = request.AgentId;
        if (request.DueAt is not null) entity.DueAt = request.DueAt;

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToRecord(entity);
    }

    private static ActionRecord ToRecord(ActionEntity e) => new(
        e.Id, e.TenantId, e.TaskId, e.AgentId, e.CreatedByAgentId,
        e.Title, e.Description, e.Status, e.Priority, e.ParentActionId,
        e.DueAt, e.CompletedAt, e.CreatedAt, e.UpdatedAt);
}
