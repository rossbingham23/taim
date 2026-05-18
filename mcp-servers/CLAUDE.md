# TAIM MCP Servers

Three MCP (Model Context Protocol) servers using **stdio transport**. They are NOT standalone Docker services — the API container spawns them as subprocesses.

## Available Servers

| Directory | Purpose | Tools exposed |
|---|---|---|
| `web-search/` | DuckDuckGo web search | `search` |
| `email/` | Email composition (stub) | `send_email` |
| `github/` | GitHub API queries | `get_repo`, `search_issues` |

## Transport

All servers use `StdioServerTransport` from the MCP SDK. They communicate via stdin/stdout. There is no HTTP port, no Docker networking — just a process with pipes.

## How They Are Spawned

`Taim.Connectors/McpStdioConnector.cs` spawns a server as a subprocess:

```csharp
var process = new Process { StartInfo = new ProcessStartInfo {
    FileName = "node",
    Arguments = "/app/mcp-servers/web-search/index.js",
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    UseShellExecute = false,
}};
process.Start();
var transport = new StdioClientTransport(process.StandardInput, process.StandardOutput);
var client = new McpClient(transport);
```

The connector is registered as `AddScoped<McpStdioConnector>()` via `AddTaimConnectors()` in `Taim.Connectors/ConnectorExtensions.cs`.

## Adding a New MCP Server

1. Create `mcp-servers/my-server/index.js` — implement using `@modelcontextprotocol/sdk`
2. Add `package.json` with the dependency
3. Run `npm install` in that directory
4. Copy server files into the Docker image — update `src/backend/Dockerfile` `COPY` step
5. Register a new connector or extend `McpStdioConnector` to support the new server path
6. Expose the tools to agents through the relevant agent's tool list

## docker-compose Notes

The `mcp-servers` containers in `docker-compose.yml` have `restart: "no"` — they are there only as build artifacts to run `npm install`. They are NOT started as services; ignore them when checking container health.
