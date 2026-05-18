using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Taim.Core.Providers;

namespace Taim.Data.Services;

public sealed class TenantProviderResolver(TaimDbContext db, IConfiguration config) : ITenantProviderResolver
{
    public LlmProviderConfig Resolve(Guid tenantId, string? preferredProvider = null)
    {
        var configs = db.TenantProviderConfigs
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .ToList();

        if (preferredProvider is not null)
        {
            var preferred = configs.FirstOrDefault(c =>
                c.Provider.Equals(preferredProvider, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
                return Map(preferred);
        }

        foreach (var provider in new[] { "anthropic", "openai", "gemini", "ollama" })
        {
            var match = configs.FirstOrDefault(c =>
                c.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return Map(match);
        }

        // No DB config — fall back to environment variables
        return ResolveFromEnvironment(preferredProvider);
    }

    private LlmProviderConfig ResolveFromEnvironment(string? preferredProvider)
    {
        var anthropicKey = config["ANTHROPIC_API_KEY"];
        var openaiKey    = config["OPENAI_API_KEY"];
        var geminiKey    = config["GOOGLE_API_KEY"];
        var ollamaUrl    = config["OLLAMA_BASE_URL"]
                       ?? config["LlmProviders:Ollama:BaseUrl"]
                       ?? "http://host.docker.internal:11434";

        if (preferredProvider is not null)
        {
            return preferredProvider.ToLowerInvariant() switch
            {
                "anthropic" when !string.IsNullOrEmpty(anthropicKey)
                    => new LlmProviderConfig("anthropic", "claude-sonnet-4-6", anthropicKey, null),
                "openai" when !string.IsNullOrEmpty(openaiKey)
                    => new LlmProviderConfig("openai", "gpt-4o", openaiKey, null),
                "gemini" when !string.IsNullOrEmpty(geminiKey)
                    => new LlmProviderConfig("gemini", "gemini-2.0-flash", geminiKey, null),
                _ => new LlmProviderConfig("ollama", "llama3.2", null, ollamaUrl),
            };
        }

        // Priority: Anthropic → OpenAI → Gemini → Ollama
        if (!string.IsNullOrEmpty(anthropicKey))
            return new LlmProviderConfig("anthropic", "claude-sonnet-4-6", anthropicKey, null);
        if (!string.IsNullOrEmpty(openaiKey))
            return new LlmProviderConfig("openai", "gpt-4o", openaiKey, null);
        if (!string.IsNullOrEmpty(geminiKey))
            return new LlmProviderConfig("gemini", "gemini-2.0-flash", geminiKey, null);

        return new LlmProviderConfig("ollama", "llama3.2", null, ollamaUrl);
    }

    private static LlmProviderConfig Map(Models.TenantProviderConfigEntity e) =>
        new(e.Provider, e.DefaultModel, e.ApiKey, e.BaseUrl);
}
