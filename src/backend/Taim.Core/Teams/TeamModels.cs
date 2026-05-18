namespace Taim.Core.Teams;

public sealed record TaskRecord(
    Guid Id,
    Guid TenantId,
    string Goal,
    string Status,
    Guid? BudgetId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record CreateTaskRequest(Guid TenantId, string Goal, decimal BudgetLimitUsd, string? Provider = null, string? Model = null);

public sealed record TeamGraph(Guid TaskId, IReadOnlyList<TeamNode> Nodes, IReadOnlyList<TeamEdge> Edges);

public sealed record TeamNode(
    Guid AgentId,
    string Name,
    string Role,
    string Status,
    int Depth,
    IReadOnlyList<Guid> KpiIds
);

public sealed record TeamEdge(Guid ParentAgentId, Guid ChildAgentId);

public interface ITaskService
{
    Task<TaskRecord> CreateAsync(CreateTaskRequest request, CancellationToken ct = default);
    Task<TaskRecord?> GetAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskRecord>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
    Task<TeamGraph> GetTeamGraphAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);
    Task UpdateStatusAsync(Guid tenantId, Guid taskId, string status, CancellationToken ct = default);
}
