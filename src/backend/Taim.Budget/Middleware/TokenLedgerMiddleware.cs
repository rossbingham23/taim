using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Taim.Core.Budget;

namespace Taim.Budget.Middleware;

/// <summary>
/// MEAI middleware that intercepts every LLM call to count tokens and enforce budget limits.
/// Uses IServiceScopeFactory so each call gets a fresh IBudgetService (and TaimDbContext),
/// preventing EF Core concurrency errors when multiple agents call the LLM in parallel.
/// </summary>
public sealed class TokenLedgerMiddleware(
    IChatClient innerClient,
    IServiceScopeFactory scopeFactory,
    IPricingCardProvider pricingCards,
    Guid tenantId,
    Guid agentId,
    Guid budgetId,
    string provider,
    string model) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var budgetService = scope.ServiceProvider.GetRequiredService<IBudgetService>();

        var pricing = pricingCards.Get(provider, model);
        if (pricing is not null)
        {
            int estimatedInputTokens = messages.Sum(m => m.Text?.Length / 4 ?? 0);
            decimal estimatedCost = (estimatedInputTokens / 1000m) * pricing.InputCostPer1kTokens;
            bool canAfford = await budgetService.CanAffordAsync(tenantId, budgetId, estimatedCost, cancellationToken);
            if (!canAfford)
                throw new BudgetExceededException(tenantId, budgetId, estimatedCost);
        }

        var response = await base.GetResponseAsync(messages, options, cancellationToken);

        if (response.Usage is { } usage)
        {
            await budgetService.RecordSpendAsync(
                tenantId, budgetId, agentId, provider, model,
                (int)(usage.InputTokenCount ?? 0), (int)(usage.OutputTokenCount ?? 0),
                cancellationToken);
        }

        return response;
    }
}

public sealed class BudgetExceededException(Guid tenantId, Guid budgetId, decimal estimatedCostUsd)
    : Exception($"Budget {budgetId} for tenant {tenantId} would be exceeded by ~${estimatedCostUsd:F4}.")
{
    public Guid TenantId { get; } = tenantId;
    public Guid BudgetId { get; } = budgetId;
    public decimal EstimatedCostUsd { get; } = estimatedCostUsd;
}

public interface IPricingCardProvider
{
    PricingCard? Get(string provider, string model);
}
