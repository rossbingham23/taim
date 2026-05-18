using Taim.Core.Budget;
using Taim.Budget.Middleware;

namespace Taim.Budget.Models;

/// <summary>
/// Default pricing table based on published API prices (per 1k tokens).
/// Values are approximations — update when providers change pricing.
/// </summary>
public sealed class DefaultPricingCardProvider : IPricingCardProvider
{
    private static readonly Dictionary<string, PricingCard> Cards = new(StringComparer.OrdinalIgnoreCase)
    {
        // OpenAI
        ["openai:gpt-4o"]            = new("openai", "gpt-4o",            0.0025m, 0.010m),
        ["openai:gpt-4o-mini"]       = new("openai", "gpt-4o-mini",       0.000150m, 0.000600m),
        ["openai:gpt-4-turbo"]       = new("openai", "gpt-4-turbo",       0.010m, 0.030m),
        ["openai:gpt-3.5-turbo"]     = new("openai", "gpt-3.5-turbo",     0.0005m, 0.0015m),

        // Anthropic
        ["anthropic:claude-opus-4-7"]      = new("anthropic", "claude-opus-4-7",      0.015m, 0.075m),
        ["anthropic:claude-sonnet-4-6"]    = new("anthropic", "claude-sonnet-4-6",    0.003m, 0.015m),
        ["anthropic:claude-haiku-4-5-20251001"] = new("anthropic", "claude-haiku-4-5-20251001", 0.00025m, 0.00125m),

        // Gemini
        ["gemini:gemini-2.0-flash"]        = new("gemini", "gemini-2.0-flash",        0.000075m, 0.000300m),
        ["gemini:gemini-2.5-pro"]          = new("gemini", "gemini-2.5-pro",          0.00125m, 0.010m),

        // Ollama — zero cost (local)
        ["ollama:*"]                       = new("ollama", "*", 0m, 0m),
    };

    public PricingCard? Get(string provider, string model)
    {
        var key = $"{provider.ToLowerInvariant()}:{model}";
        if (Cards.TryGetValue(key, out var card)) return card;

        // Fallback: provider wildcard
        var wildcardKey = $"{provider.ToLowerInvariant()}:*";
        return Cards.GetValueOrDefault(wildcardKey);
    }
}
