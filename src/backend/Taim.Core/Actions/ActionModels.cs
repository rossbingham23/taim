namespace Taim.Core.Actions;

public sealed record ActionRecord(
    Guid Id,
    Guid TenantId,
    Guid TaskId,
    Guid? AgentId,
    Guid? CreatedByAgentId,
    string Title,
    string? Description,
    string Status,
    int Priority,
    Guid? ParentActionId,
    DateTimeOffset? DueAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record CreateActionRequest(
    Guid TenantId,
    Guid TaskId,
    Guid? AgentId,
    Guid? CreatedByAgentId,
    string Title,
    string? Description = null,
    int Priority = 50,
    Guid? ParentActionId = null,
    DateTimeOffset? DueAt = null
);

public sealed record UpdateActionRequest(
    string? Status = null,
    string? Title = null,
    string? Description = null,
    int? Priority = null,
    Guid? AgentId = null,
    DateTimeOffset? DueAt = null
);

public interface IActionService
{
    Task<ActionRecord> CreateAsync(CreateActionRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ActionRecord>> GetForTaskAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);
    Task<ActionRecord?> GetAsync(Guid tenantId, Guid actionId, CancellationToken ct = default);
    Task<ActionRecord> UpdateAsync(Guid tenantId, Guid actionId, UpdateActionRequest request, CancellationToken ct = default);
}
