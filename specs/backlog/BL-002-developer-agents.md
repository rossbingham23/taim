---
id: BL-002
title: Developer Agents with ClaudeCode Connector
sprint: 4
status: draft
created: 2026-05-18
updated: 2026-05-18
---

# BL-002 — Developer Agents with ClaudeCode Connector

## Problem Statement

The `ClaudeCodeConnector` exists and can spawn `claude --mcp-server` as a subprocess. Developer and QA agents exist as roles. But the connector is never given to those agents — they have no tools when they execute actions. A Developer agent with the ClaudeCode tool can write, edit, and test code. Without it, Developer agents are useless.

## Solution Overview

Wire `ClaudeCodeConnector` to Developer and QA roles in the agent execution engine. Add `WebSearchConnector` to executive roles. Introduce `ConnectorRegistry.GetToolsForRole(AgentRole)` to centralize the role → tool mapping.

## Tool Assignment by Role

| Role | Tools |
|---|---|
| Developer, QA Engineer | `claude_code`, `web_search` |
| QA Manager | `web_search` |
| CEO, CTO, CMO, CFO, HR | `web_search` |
| Designer, Product Manager | `web_search` |
| Data Analyst | `web_search` |
| Others | (none) |

## ConnectorRegistry

New service in `Taim.Connectors/ConnectorRegistry.cs`:
```csharp
public interface IConnectorRegistry
{
    Task<IList<AITool>> GetToolsForRoleAsync(AgentRole role, CancellationToken ct = default);
}
```

Registered in `ConnectorExtensions.AddTaimConnectors()`.

## ClaudeCode Connector Notes

The `ClaudeCode` MCP server is the `claude` CLI binary. It must be installed in the API container. Verify `claude --version` works in the Docker container. If not, add `RUN npm install -g @anthropic-ai/claude-code` to the Dockerfile.

The connector works directory-scoped — pass `--workdir /app/workspace` (or similar) so the agent has a defined working directory for code output.

## Approval Gate

Any `claude_code` tool call that writes files or runs commands MUST go through the approval gate. The approval description should clearly state: file to be written/command to be run.

## Dependencies

- Agent work loop (BL-001) must exist before this is useful
- `ClaudeCodeConnector` already exists in `Taim.Connectors/ClaudeCode/`
- `WebSearchConnector` already exists in `Taim.Connectors/WebSearch/`

## Spec Status: Draft

Full spec to be written at the start of Sprint 4.
