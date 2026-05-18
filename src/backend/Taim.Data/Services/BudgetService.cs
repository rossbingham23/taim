using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Taim.Core.Budget;
using Taim.Data.Models;

namespace Taim.Data.Services;

public sealed class BudgetService(TaimDbContext db, IConnectionMultiplexer redis) : IBudgetService
{
    private IDatabase Cache => redis.GetDatabase();

    public async Task<BudgetRecord> CreateAsync(Guid tenantId, Guid? taskId, decimal limitUsd, CancellationToken ct = default)
    {
        var entity = new BudgetEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaskId = taskId,
            LimitUsd = limitUsd,
            SpentUsd = 0,
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Budgets.Add(entity);
        await db.SaveChangesAsync(ct);
        return MapToRecord(entity);
    }

    public async Task<bool> CanAffordAsync(Guid tenantId, Guid budgetId, decimal estimatedCostUsd, CancellationToken ct = default)
    {
        // Fast path: check Redis atomic counter first
        var redisKey = RedisKey(tenantId, budgetId);
        var cachedSpent = await Cache.StringGetAsync(redisKey);
        if (cachedSpent.HasValue && decimal.TryParse(cachedSpent.ToString(), out var spent))
        {
            var budget = await db.Budgets.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == budgetId && b.TenantId == tenantId, ct);
            if (budget is null || budget.Status == "exhausted") return false;
            return spent + estimatedCostUsd <= budget.LimitUsd;
        }

        // Fallback: read from DB
        var b = await db.Budgets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == budgetId && x.TenantId == tenantId, ct);
        if (b is null || b.Status == "exhausted") return false;
        return b.SpentUsd + estimatedCostUsd <= b.LimitUsd;
    }

    public async Task RecordSpendAsync(
        Guid tenantId, Guid budgetId, Guid agentId,
        string provider, string model,
        int inputTokens, int outputTokens,
        CancellationToken ct = default)
    {
        var budget = await db.Budgets.FirstOrDefaultAsync(b => b.Id == budgetId && b.TenantId == tenantId, ct);
        if (budget is null) return;

        // Rough cost: use a default pricing if no card found
        decimal costUsd = CalculateCost(provider, model, inputTokens, outputTokens);

        var entry = new SpendEntryEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BudgetId = budgetId,
            AgentId = agentId,
            Provider = provider,
            Model = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CostUsd = costUsd,
            RecordedAt = DateTimeOffset.UtcNow,
        };
        db.SpendEntries.Add(entry);

        budget.SpentUsd += costUsd;
        budget.UpdatedAt = DateTimeOffset.UtcNow;
        if (budget.SpentUsd >= budget.LimitUsd)
            budget.Status = "exhausted";

        await db.SaveChangesAsync(ct);

        // Update Redis counter for fast pre-call checks
        await Cache.StringSetAsync(RedisKey(tenantId, budgetId), budget.SpentUsd.ToString(), TimeSpan.FromMinutes(5));
    }

    public async Task<BudgetReport> GetReportAsync(Guid tenantId, Guid budgetId, CancellationToken ct = default)
    {
        var budget = await db.Budgets.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == budgetId && b.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Budget {budgetId} not found.");

        var entries = await db.SpendEntries.AsNoTracking()
            .Where(e => e.BudgetId == budgetId && e.TenantId == tenantId)
            .ToListAsync(ct);

        var agents = await db.Agents.AsNoTracking()
            .Where(a => entries.Select(e => e.AgentId).Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Name, ct);

        var byAgent = entries
            .GroupBy(e => e.AgentId)
            .Select(g => new AgentSpendSummary(
                g.Key,
                agents.GetValueOrDefault(g.Key, g.Key.ToString()),
                g.Sum(e => e.CostUsd),
                g.Sum(e => (long)(e.InputTokens + e.OutputTokens))
            ))
            .ToList();

        return new BudgetReport(
            budget.Id,
            budget.LimitUsd,
            budget.SpentUsd,
            Math.Max(0, budget.LimitUsd - budget.SpentUsd),
            Enum.Parse<BudgetStatus>(budget.Status, ignoreCase: true),
            byAgent
        );
    }

    public async Task<BudgetRecord?> GetAsync(Guid tenantId, Guid budgetId, CancellationToken ct = default)
    {
        var entity = await db.Budgets.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == budgetId && b.TenantId == tenantId, ct);
        return entity is null ? null : MapToRecord(entity);
    }

    private static string RedisKey(Guid tenantId, Guid budgetId) =>
        $"taim:budget:{tenantId}:{budgetId}:spent";

    private static decimal CalculateCost(string provider, string model, int inputTokens, int outputTokens)
    {
        // Default pricing in USD per 1K tokens — override via PricingCard in DB
        var (inputRate, outputRate) = (provider.ToLower(), model.ToLower()) switch
        {
            ("anthropic", _) when model.Contains("opus") => (0.015m, 0.075m),
            ("anthropic", _) when model.Contains("sonnet") => (0.003m, 0.015m),
            ("anthropic", _) => (0.00025m, 0.00125m),
            ("openai", _) when model.Contains("gpt-4o") => (0.005m, 0.015m),
            ("openai", _) when model.Contains("gpt-4") => (0.01m, 0.03m),
            ("openai", _) => (0.0005m, 0.0015m),
            ("gemini", _) when model.Contains("pro") => (0.00125m, 0.005m),
            ("gemini", _) => (0.000075m, 0.0003m),
            ("ollama", _) => (0m, 0m), // local models are free
            _ => (0.001m, 0.003m),
        };
        return (inputTokens / 1000m * inputRate) + (outputTokens / 1000m * outputRate);
    }

    private static BudgetRecord MapToRecord(BudgetEntity e) => new(
        e.Id, e.TenantId, e.TaskId,
        e.LimitUsd, e.SpentUsd,
        Enum.Parse<BudgetStatus>(e.Status, ignoreCase: true),
        e.CreatedAt, e.UpdatedAt
    );
}
