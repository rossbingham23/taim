using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Taim.Agents.Bootstrap;
using Taim.Budget.Middleware;
using Taim.Core.Agents;
using Taim.Core.Notifications;
using Taim.Core.Providers;

namespace Taim.Agents.Shared;

/// <summary>
/// Creates and initializes agent instances for a given tenant and task.
/// Responsible for:
/// - Registering the agent in the database
/// - Resolving the LLM provider for the agent
/// - Wrapping the IChatClient with budget tracking middleware
/// - Notifying the frontend of new agents via SignalR
/// </summary>
public sealed class AgentFactory(
    IAgentRegistry registry,
    IProviderFactory providerFactory,
    IServiceScopeFactory scopeFactory,
    IPricingCardProvider pricingCards,
    INotificationService notifications,
    ILogger<AgentFactory> logger)
{
    public async Task<(AgentDefinition Definition, IChatClient ChatClient)> CreateAsync(
        CreateAgentRequest request,
        CancellationToken ct = default)
    {
        logger.LogInformation("Creating agent {Name} ({Role}) for tenant {TenantId}",
            request.Name, request.Role, request.TenantId);

        var definition = await registry.RegisterAsync(request, ct);

        var providerConfig = providerFactory.ResolveConfig(request.TenantId, request.Provider);
        var model = request.Model ?? providerConfig.DefaultModel;
        var rawClient = providerFactory.CreateChatClient(request.TenantId, request.Provider, request.Model);

        IChatClient chatClient = rawClient;
        if (request.BudgetId is { } budgetId)
        {
            chatClient = new ChatClientBuilder(rawClient)
                .Use(inner => new TokenLedgerMiddleware(
                    inner,
                    scopeFactory,
                    pricingCards,
                    request.TenantId,
                    definition.Id,
                    budgetId,
                    providerConfig.Provider,
                    model))
                .Build();
        }

        await notifications.NotifyAsync(
            request.TenantId,
            NotificationKind.TeamUpdate,
            $"Agent Created: {request.Name}",
            $"{request.Role} agent registered and ready.",
            new Dictionary<string, object?> { ["agentId"] = definition.Id.ToString(), ["role"] = request.Role.ToString() },
            ct);

        return (definition, chatClient);
    }

    public async Task<IReadOnlyList<(AgentDefinition Definition, IChatClient ChatClient)>> CreateTeamAsync(
        Guid tenantId,
        Guid taskId,
        IEnumerable<CreateAgentRequest> agentRequests,
        CancellationToken ct = default)
    {
        var results = new List<(AgentDefinition, IChatClient)>();
        foreach (var request in agentRequests)
        {
            var agent = await CreateAsync(request with { TaskId = taskId }, ct);
            results.Add(agent);
        }
        return results;
    }

    /// <summary>
    /// Creates the initial executive team from a BootstrapAgent recommendation.
    /// </summary>
    public async Task<IReadOnlyList<(AgentDefinition Definition, IChatClient ChatClient)>> CreateFromRecommendationAsync(
        Guid tenantId,
        Guid taskId,
        Guid? budgetId,
        TeamRecommendation recommendation,
        CancellationToken ct = default)
    {
        if (recommendation.ExecutiveTeam is not { Count: > 0 })
            throw new InvalidOperationException("Bootstrap returned an empty executive team.");

        var requests = recommendation.ExecutiveTeam.Select(spec => new CreateAgentRequest(
            TenantId: tenantId,
            TaskId: taskId,
            ParentAgentId: null,
            Name: spec.Name,
            Role: MapRole(spec.Role),
            Charter: spec.Charter,
            Provider: spec.PreferredProvider,
            Model: spec.PreferredModel,
            BudgetId: budgetId));

        return await CreateTeamAsync(tenantId, taskId, requests, ct);
    }

    private static AgentRole MapRole(string role) => role.ToLowerInvariant() switch
    {
        "ceo" => AgentRole.Ceo,
        "cto" => AgentRole.Cto,
        "cmo" => AgentRole.Cmo,
        "cfo" => AgentRole.Cfo,
        "hr" or "chro" or "headofhr" => AgentRole.Hr,
        "productmanager" or "pm" => AgentRole.ProductManager,
        "developer" or "dev" or "engineer" => AgentRole.Developer,
        "designer" or "ux" or "ui" => AgentRole.Designer,
        "qaengineer" or "qa" => AgentRole.QaEngineer,
        "qamanager" => AgentRole.QaManager,
        "marketingspecialist" or "marketing" => AgentRole.MarketingSpecialist,
        "contentwriter" => AgentRole.ContentWriter,
        "dataanalyst" => AgentRole.DataAnalyst,
        "salesrepresentative" or "sales" => AgentRole.SalesRepresentative,
        "customersupport" or "support" => AgentRole.CustomerSupport,
        _ => AgentRole.Generic
    };
}
