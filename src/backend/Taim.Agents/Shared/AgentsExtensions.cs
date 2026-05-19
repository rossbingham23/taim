using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Taim.Agents.Bootstrap;
using Taim.Agents.Expert;
using Taim.Agents.Meetings;
using Taim.Core.Meetings;
using Taim.Core.Providers;

namespace Taim.Agents.Shared;

public static class AgentsExtensions
{
    public static IServiceCollection AddTaimAgents(this IServiceCollection services)
    {
        // Global IChatClient — resolved using env-var config (Guid.Empty → fallback to ANTHROPIC_API_KEY etc.)
        // BootstrapAgent and ExpertAgent use this; per-agent clients are created by AgentFactory.
        services.AddScoped<IChatClient>(sp =>
        {
            var factory = sp.GetRequiredService<IProviderFactory>();
            return factory.CreateChatClient(Guid.Empty);
        });

        services.AddScoped<AgentFactory>();
        services.AddScoped<AgentOrchestrator>();
        services.AddScoped<BootstrapAgent>();
        services.AddScoped<ExpertAgent>();
        services.AddScoped<IMeetingOrchestrator, MeetingOrchestrator>();
        services.AddScoped<IActionExecutor, ActionExecutor>();

        // Executive and domain agents are NOT registered in DI — AgentFactory instantiates
        // them directly (new CeoAgent(chatClient)) with per-tenant, per-budget IChatClient wrappers.

        return services;
    }
}
