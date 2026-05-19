using Microsoft.Extensions.DependencyInjection;
using Taim.Connectors.ClaudeCode;
using Taim.Connectors.Email;
using Taim.Connectors.Sdk;
using Taim.Connectors.WebSearch;

namespace Taim.Connectors;

public static class ConnectorExtensions
{
    public static IServiceCollection AddTaimConnectors(this IServiceCollection services)
    {
        services.AddSingleton<IConnector, WebSearchConnector>();
        services.AddSingleton<IConnector, EmailConnector>();
        services.AddSingleton<IConnector, ClaudeCodeConnector>();
        services.AddConnectors();
        return services;
    }
}
