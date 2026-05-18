using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace Taim.Connectors.Sdk;

/// <summary>
/// A connector wraps an MCP server and exposes its tools as AITool instances
/// that can be added to any agent.
///
/// To add a new connector:
///   1. Create a new folder under Taim.Connectors/ (e.g. Taim.Connectors/MyConnector/)
///   2. Implement IConnector
///   3. Register in ConnectorRegistry
///   4. The connector's MCP server (TypeScript/Python/C#) lives in /mcp-servers/
///   5. Run the test suite in Taim.Tests/Connectors/ to verify end-to-end
/// </summary>
public interface IConnector
{
    string ConnectorId { get; }
    string DisplayName { get; }
    string Description { get; }

    /// <summary>
    /// Returns the AITool instances this connector provides.
    /// Called once when an agent is initialized with this connector.
    /// </summary>
    Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken ct = default);

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}

/// <summary>
/// Base class for MCP-based connectors (stdio transport).
/// McpClientTool extends AITool so the tools list is directly usable as ChatOptions.Tools.
/// </summary>
public abstract class McpStdioConnector : IConnector, IAsyncDisposable
{
    private McpClient? _mcpClient;
    private IReadOnlyList<AITool>? _tools;

    public abstract string ConnectorId { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    protected abstract string Command { get; }
    protected abstract IEnumerable<string> Arguments { get; }
    protected virtual IDictionary<string, string?>? Environment => null;

    public async Task StartAsync(CancellationToken ct = default)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = ConnectorId,
            Command = Command,
            Arguments = [..Arguments],
            EnvironmentVariables = Environment
        });
        var client = await McpClient.CreateAsync(transport, cancellationToken: ct);
        _mcpClient = client;
        var mcpTools = await client.ListToolsAsync(cancellationToken: ct);
        _tools = [..mcpTools];
    }

    public Task StopAsync(CancellationToken ct = default) => DisposeAsync().AsTask();

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken ct = default) =>
        Task.FromResult(_tools ?? throw new InvalidOperationException($"Connector '{ConnectorId}' has not been started."));

    public async ValueTask DisposeAsync()
    {
        if (_mcpClient is not null) await _mcpClient.DisposeAsync();
    }
}
