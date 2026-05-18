using Microsoft.Extensions.AI;

namespace Taim.Core.Providers;

public sealed record LlmProviderConfig(
    string Provider,
    string DefaultModel,
    string? ApiKey = null,
    string? BaseUrl = null
);

public interface ITenantProviderResolver
{
    LlmProviderConfig Resolve(Guid tenantId, string? preferredProvider = null);
}

public interface IProviderFactory
{
    IChatClient CreateChatClient(Guid tenantId, string? preferredProvider = null, string? preferredModel = null);
    LlmProviderConfig ResolveConfig(Guid tenantId, string? preferredProvider = null);
}
