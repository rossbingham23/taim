using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;

namespace Taim.Connectors.Sdk;

public interface IConnectorRegistry
{
    IReadOnlyList<IConnector> GetAll();
    IConnector? Get(string connectorId);
    Task<IReadOnlyList<AITool>> GetToolsForAgentAsync(IEnumerable<string> connectorIds, CancellationToken ct = default);
}

public sealed class ConnectorRegistry(IEnumerable<IConnector> connectors) : IConnectorRegistry
{
    private readonly Dictionary<string, IConnector> _connectors =
        connectors.ToDictionary(c => c.ConnectorId);

    private readonly HashSet<string> _started = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _lock = new(1, 1);

    public IReadOnlyList<IConnector> GetAll() => [.._connectors.Values];

    public IConnector? Get(string connectorId) =>
        _connectors.GetValueOrDefault(connectorId);

    public async Task<IReadOnlyList<AITool>> GetToolsForAgentAsync(
        IEnumerable<string> connectorIds, CancellationToken ct = default)
    {
        var tools = new List<AITool>();
        foreach (var id in connectorIds)
        {
            if (!_connectors.TryGetValue(id, out var connector)) continue;
            await EnsureStartedAsync(connector, ct);
            tools.AddRange(await connector.GetToolsAsync(ct));
        }
        return tools;
    }

    private async Task EnsureStartedAsync(IConnector connector, CancellationToken ct)
    {
        if (_started.Contains(connector.ConnectorId)) return;

        await _lock.WaitAsync(ct);
        try
        {
            if (_started.Contains(connector.ConnectorId)) return;
            await connector.StartAsync(ct);
            _started.Add(connector.ConnectorId);
        }
        finally
        {
            _lock.Release();
        }
    }
}

public static class ConnectorRegistryExtensions
{
    public static IServiceCollection AddConnectors(this IServiceCollection services)
    {
        services.AddSingleton<IConnectorRegistry, ConnectorRegistry>();
        return services;
    }
}
