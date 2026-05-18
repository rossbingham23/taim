using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.ClientModel;
using Taim.Core.Providers;

namespace Taim.Providers;

public sealed class ProviderFactory(IServiceProvider services) : IProviderFactory
{
    public LlmProviderConfig ResolveConfig(Guid tenantId, string? preferredProvider = null)
    {
        var resolver = services.GetRequiredService<ITenantProviderResolver>();
        return resolver.Resolve(tenantId, preferredProvider);
    }

    public IChatClient CreateChatClient(Guid tenantId, string? preferredProvider = null, string? preferredModel = null)
    {
        var config = ResolveConfig(tenantId, preferredProvider);
        var model = preferredModel ?? config.DefaultModel;

        return config.Provider.ToLowerInvariant() switch
        {
            "openai" => CreateOpenAI(config, model),
            "anthropic" => CreateAnthropic(config, model),
            "gemini" => CreateGemini(config, model),
            "ollama" => CreateOllama(config, model),
            _ => throw new NotSupportedException($"LLM provider '{config.Provider}' is not supported.")
        };
    }

    private static IChatClient CreateOpenAI(LlmProviderConfig config, string model)
    {
        var key = config.ApiKey ?? throw new InvalidOperationException("OpenAI API key is required.");
        var client = new OpenAI.OpenAIClient(key);
        return client.GetChatClient(model).AsIChatClient();
    }

    private static IChatClient CreateAnthropic(LlmProviderConfig config, string model)
    {
        var key = config.ApiKey ?? throw new InvalidOperationException("Anthropic API key is required.");
        var endpoint = new Uri(config.BaseUrl ?? "https://api.anthropic.com/v1");
        var options = new OpenAI.OpenAIClientOptions { Endpoint = endpoint };
        var client = new OpenAI.OpenAIClient(new ApiKeyCredential(key), options);
        return client.GetChatClient(model).AsIChatClient();
    }

    private static IChatClient CreateGemini(LlmProviderConfig config, string model)
    {
        var key = config.ApiKey ?? throw new InvalidOperationException("Google API key is required.");
        var endpoint = new Uri(config.BaseUrl ?? "https://generativelanguage.googleapis.com/v1beta/openai/");
        var options = new OpenAI.OpenAIClientOptions { Endpoint = endpoint };
        var client = new OpenAI.OpenAIClient(new ApiKeyCredential(key), options);
        return client.GetChatClient(model).AsIChatClient();
    }

    private static IChatClient CreateOllama(LlmProviderConfig config, string model)
    {
        var baseUrl = config.BaseUrl ?? "http://localhost:11434";
        return new OllamaChatClient(new Uri(baseUrl), model);
    }
}

public static class ProviderFactoryExtensions
{
    public static IServiceCollection AddTaimProviders(this IServiceCollection services)
    {
        // Scoped (not Singleton) — needs to resolve ITenantProviderResolver which is Scoped.
        services.AddScoped<IProviderFactory, ProviderFactory>();
        return services;
    }
}
