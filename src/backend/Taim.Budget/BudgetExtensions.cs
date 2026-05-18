using Microsoft.Extensions.DependencyInjection;
using Taim.Budget.Middleware;
using Taim.Budget.Models;

namespace Taim.Budget;

public static class BudgetExtensions
{
    public static IServiceCollection AddTaimBudget(this IServiceCollection services)
    {
        services.AddSingleton<IPricingCardProvider, DefaultPricingCardProvider>();
        return services;
    }
}
