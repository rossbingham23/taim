# Taim.Connectors — External Tool Connectors

MCP (Model Context Protocol) and direct tool integrations for agents. Spawns external tools as subprocesses.

## Key Files

| File | Purpose |
|---|---|
| `Sdk/McpStdioConnector.cs` | Base class — spawns an MCP server as a stdio subprocess and exposes its tools as `AITool` list |
| `ClaudeCode/ClaudeCodeConnector.cs` | MCP connector for the `claude` CLI — Developer/QA agents use this to write code |
| `WebSearch/WebSearchConnector.cs` | MCP connector for the web-search MCP server |
| `Email/EmailConnector.cs` | MCP connector for the email MCP server |
| `GitHub/GitHubConnector.cs` | MCP connector for the GitHub MCP server |
| `ConnectorExtensions.cs` | `AddTaimConnectors()` — registers connector factories |

## MCP Transport

All connectors use **stdio transport only** — each connector spawns the MCP server as a child process when first used. The MCP servers are Node.js processes in `mcp-servers/` (deployed inside the API container).

Do NOT use HTTP/SSE transport for MCP servers — the architecture assumes subprocess spawning.

## ClaudeCode Connector

`ClaudeCodeConnector` runs `claude --mcp-server` as a subprocess. It exposes a `claude_code` tool that lets Developer agents write, read, and edit files.

**Status (Sprint 1)**: Wired but not yet given to agents. Sprint 3/4 work: pass `ClaudeCodeConnector.GetToolsAsync()` to Developer agents in `AgentOrchestrator.ExecuteActionAsync`.

## Adding a New Connector

1. Create `NewTool/NewToolConnector.cs` extending `McpStdioConnector`
2. Override `ServerExecutable` and `ServerArgs` to point at the MCP server process
3. Register in `ConnectorExtensions.cs`
4. Wire to appropriate agent roles in `AgentOrchestrator` when the work loop is built (Sprint 3)

## Tool Assignment by Role (Sprint 3, via ConnectorMapping in Taim.Agents)

Connector IDs are resolved in `Taim.Agents/Shared/ConnectorMapping.cs` → `GetConnectorIds(AgentRole)`:

| Role | Connector IDs |
|---|---|
| `Developer`, `QaEngineer`, `QaManager` | `["web-search", "claude-code"]` |
| All others (executive roles) | `["web-search"]` |

`ClaudeCodeConnector` is registered as Singleton in `ConnectorExtensions.AddTaimConnectors()`.
