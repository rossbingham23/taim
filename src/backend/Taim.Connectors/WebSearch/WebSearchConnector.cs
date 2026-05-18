using Microsoft.Extensions.Configuration;
using Taim.Connectors.Sdk;

namespace Taim.Connectors.WebSearch;

public sealed class WebSearchConnector(IConfiguration config) : McpStdioConnector
{
    public override string ConnectorId => "web-search";
    public override string DisplayName => "Web Search";
    public override string Description => "Searches the web using the Brave Search API.";

    protected override string Command => "node";
    protected override IEnumerable<string> Arguments => ["mcp-servers/web-search/index.js"];

    protected override IDictionary<string, string?>? Environment
    {
        get
        {
            var key = config["Brave:ApiKey"];
            return key is not null
                ? new Dictionary<string, string?> { ["BRAVE_API_KEY"] = key }
                : null;
        }
    }
}
