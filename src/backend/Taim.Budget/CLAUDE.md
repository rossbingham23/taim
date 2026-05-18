# Taim.Budget — Token Spend Tracking

Wraps `IChatClient` with spend tracking middleware. Enforces per-task budget limits.

## Key Files

| File | Purpose |
|---|---|
| `Middleware/TokenLedgerMiddleware.cs` | `IChatClient` decorator — counts tokens, records spend, enforces limits |
| `Ledger/` | Pricing card provider (cost-per-token tables for each model) |
| `BudgetExtensions.cs` | `AddTaimBudget()` — registers pricing cards |

## How It Works

`TokenLedgerMiddleware` wraps an `IChatClient`. On every `GetResponseAsync`:
1. Checks budget via `IBudgetService.CheckAsync` (throws if exhausted)
2. Calls the inner `IChatClient`
3. Reads `response.Usage` (input/output tokens)
4. Records spend via `IBudgetService.RecordSpendAsync`

**Critical**: `TokenLedgerMiddleware` takes `IServiceScopeFactory` (NOT `IBudgetService` directly) because multiple middleware instances run concurrently in parallel kickoffs — each call creates its own short-lived scope to avoid EF Core concurrency errors.

## Registration

`TokenLedgerMiddleware` is NOT registered in DI — it is instantiated directly by `AgentFactory`:
```csharp
new TokenLedgerMiddleware(innerClient, scopeFactory, pricingCards, tenantId, agentId, budgetId, provider, model)
```

`BudgetExtensions.AddTaimBudget()` only registers `IPricingCardProvider`.

## Pricing Cards

`IPricingCardProvider` maps `(provider, model)` → `(inputCostPer1kTokens, outputCostPer1kTokens)`. Add a new model by adding an entry in `Ledger/PricingCardProvider.cs`.
