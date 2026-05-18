using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Taim.Core.Memory;
using Taim.Memory.Episodic;
using Taim.Memory.KPI;
using Taim.Memory.Semantic;
using Taim.Memory.Team;

namespace Taim.Memory;

public static class MemoryExtensions
{
    public static IServiceCollection AddTaimMemory(this IServiceCollection services)
    {
        // Embedding generator — no-op until a real embedding provider is configured.
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, NoOpEmbeddingGenerator>();

        services.AddScoped<ChatHistoryProvider>();
        services.AddScoped<KpiContextProvider>();
        services.AddScoped<TeamContextProvider>();
        services.AddScoped<IMemoryService, VectorMemoryProvider>();
        return services;
    }
}
