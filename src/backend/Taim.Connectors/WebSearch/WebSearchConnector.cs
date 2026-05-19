using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using Taim.Connectors.Sdk;

namespace Taim.Connectors.WebSearch;

public sealed class WebSearchConnector(IConfiguration config) : IConnector, IAsyncDisposable
{
    private McpClient? _mcpClient;
    private IReadOnlyList<AITool>? _tools;

    public string ConnectorId => "web-search";
    public string DisplayName => "Web Search";
    public string Description => "Searches the web using the Brave Search API.";

    public async Task StartAsync(CancellationToken ct = default)
    {
        var key = config["Brave:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            _tools = [AIFunctionFactory.Create(
                (string query) => "web search not configured: BRAVE_API_KEY is not set",
                "web_search",
                "Search the web using Brave Search API")];
            return;
        }

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = ConnectorId,
            Command = "node",
            Arguments = ["mcp-servers/web-search/index.js"],
            EnvironmentVariables = new Dictionary<string, string?> { ["BRAVE_API_KEY"] = key }
        });
        _mcpClient = await McpClient.CreateAsync(transport, cancellationToken: ct);
        var mcpTools = await _mcpClient.ListToolsAsync(cancellationToken: ct);
        _tools = [..mcpTools];
    }

    public Task StopAsync(CancellationToken ct = default) => DisposeAsync().AsTask();

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken ct = default) =>
        Task.FromResult(_tools ?? throw new InvalidOperationException("WebSearchConnector not started."));

    public async ValueTask DisposeAsync()
    {
        if (_mcpClient is not null) await _mcpClient.DisposeAsync();
    }
}
