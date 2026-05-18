namespace Taim.Core.KPIs;

public enum KpiDirection { HigherIsBetter, LowerIsBetter, TargetValue }

public sealed record KpiNode(
    Guid Id,
    Guid TenantId,
    Guid AgentId,
    Guid? ParentKpiId,
    string Name,
    string? Description,
    string? TargetValue,
    string? Unit,
    KpiDirection Direction,
    DateTimeOffset CreatedAt
);

public sealed record KpiValue(
    Guid Id,
    Guid KpiId,
    string Value,
    DateTimeOffset RecordedAt,
    string? Source
);

public sealed record KpiHierarchy(KpiNode Node, IReadOnlyList<KpiHierarchy> Children, KpiValue? LatestValue);

public sealed record CreateKpiRequest(
    Guid TenantId,
    Guid AgentId,
    Guid? ParentKpiId,
    string Name,
    string? Description,
    string? TargetValue,
    string? Unit,
    KpiDirection Direction = KpiDirection.HigherIsBetter
);

public sealed record RecordKpiValueRequest(Guid KpiId, string Value, string? Source = null);

public interface IKpiService
{
    Task<KpiNode> CreateAsync(CreateKpiRequest request, CancellationToken ct = default);
    Task RecordValueAsync(Guid tenantId, RecordKpiValueRequest request, CancellationToken ct = default);
    Task<KpiHierarchy> GetHierarchyAsync(Guid tenantId, Guid rootKpiId, CancellationToken ct = default);
    Task<IReadOnlyList<KpiNode>> GetForAgentAsync(Guid tenantId, Guid agentId, CancellationToken ct = default);
    Task<IReadOnlyList<KpiNode>> GetTaskHierarchyRootsAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);
}
