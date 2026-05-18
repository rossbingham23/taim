using Taim.Connectors.Sdk;

namespace Taim.Connectors.GitHub;

/// <summary>
/// Wraps the official @modelcontextprotocol/server-github MCP server.
/// Provides tools: create_or_update_file, get_file_contents, list_commits,
/// create_pull_request, search_repositories, and more.
///
/// Requires: GITHUB_PERSONAL_ACCESS_TOKEN environment variable.
/// </summary>
public sealed class GitHubConnector(string? personalAccessToken = null) : McpStdioConnector
{
    public override string ConnectorId => "github";
    public override string DisplayName => "GitHub";
    public override string Description => "Read and write GitHub repositories, issues, pull requests, and code.";

    protected override string Command => "npx";
    protected override IEnumerable<string> Arguments => ["-y", "@modelcontextprotocol/server-github"];
    protected override IDictionary<string, string?>? Environment =>
        personalAccessToken is not null
            ? new Dictionary<string, string?> { ["GITHUB_PERSONAL_ACCESS_TOKEN"] = personalAccessToken }
            : null;
}
