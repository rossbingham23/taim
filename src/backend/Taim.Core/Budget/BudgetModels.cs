namespace Taim.Core.Budget;

public enum BudgetStatus { Active, Paused, Exhausted }

public sealed record BudgetRecord(
    Guid Id,
    Guid TenantId,
    Guid? TaskId,
    decimal LimitUsd,
    decimal SpentUsd,
    BudgetStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record SpendEntry(
    Guid Id,
    Guid TenantId,
    Guid BudgetId,
    Guid AgentId,
    string Provider,
    string Model,
    int InputTokens,
    int OutputTokens,
    decimal CostUsd,
    DateTimeOffset RecordedAt
);

public sealed record PricingCard(string Provider, string Model, decimal InputCostPer1kTokens, decimal OutputCostPer1kTokens);

public sealed record BudgetReport(
    Guid BudgetId,
    decimal LimitUsd,
    decimal SpentUsd,
    decimal RemainingUsd,
    BudgetStatus Status,
    IReadOnlyList<AgentSpendSummary> ByAgent
);

public sealed record AgentSpendSummary(Guid AgentId, string AgentName, decimal TotalCostUsd, long TotalTokens);

public interface IBudgetService
{
    Task<BudgetRecord> CreateAsync(Guid tenantId, Guid? taskId, decimal limitUsd, CancellationToken ct = default);
    Task<bool> CanAffordAsync(Guid tenantId, Guid budgetId, decimal estimatedCostUsd, CancellationToken ct = default);
    Task RecordSpendAsync(Guid tenantId, Guid budgetId, Guid agentId, string provider, string model, int inputTokens, int outputTokens, CancellationToken ct = default);
    Task<BudgetReport> GetReportAsync(Guid tenantId, Guid budgetId, CancellationToken ct = default);
    Task<BudgetRecord?> GetAsync(Guid tenantId, Guid budgetId, CancellationToken ct = default);
}
