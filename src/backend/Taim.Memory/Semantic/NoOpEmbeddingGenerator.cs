using Microsoft.Extensions.AI;

namespace Taim.Memory.Semantic;

/// <summary>
/// Placeholder embedding generator used when no embedding API is configured.
/// Returns zero vectors — semantic search will return results ordered by insertion, not relevance.
/// Replace with a real generator (OpenAI, Anthropic, etc.) when ready.
/// </summary>
internal sealed class NoOpEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private const int Dimensions = 1536;

    public EmbeddingGeneratorMetadata Metadata { get; } =
        new("no-op", null, "no-op", Dimensions);

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = values
            .Select(_ => new Embedding<float>(new float[Dimensions]))
            .ToList();

        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
