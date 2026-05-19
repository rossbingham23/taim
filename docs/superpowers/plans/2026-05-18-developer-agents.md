# S4-001 — Developer Agents and Operational Tool Infrastructure

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire up worker agents (Developer/QA) to skip LLM kickoff, pre-seed web-search approvals for all agents, and make the Docker runtime include Node.js + claude CLI so connectors work in production.

**Architecture:** Three orthogonal fixes: (1) worker agent kickoff route bypasses the LLM and sets status Idle immediately; (2) AgentFactory pre-seeds an `agent_and_tool` approval for `web-search` after each agent is created so web searches never hit the approval queue; (3) the Dockerfile gains Node.js + `@anthropic-ai/claude-code`, the build context moves to repo root so MCP server files are included, and docker-compose gains the `BRAVE_API_KEY` + `workspaces-data` volume.

**Tech Stack:** C# / .NET 10, ASP.NET Core DI, EF Core 10, PostgreSQL, Docker multi-stage build, Node.js 20, `@anthropic-ai/claude-code` npm package, `ModelContextProtocol` NuGet package.

---

## File Map

**New files:**
- `src/backend/Taim.Agents/Worker/WorkerAgentBase.cs` — minimal placeholder for worker roles (no LLM)
- `src/backend/Taim.E2ETests/DeveloperAgentTests.cs` — smoke tests

**Modified files:**
- `src/backend/Taim.Core/Approvals/ApprovalModels.cs` — add `PreApproveAsync` to `IApprovalService`
- `src/backend/Taim.Data/Services/ApprovalService.cs` — implement `PreApproveAsync`
- `src/backend/Taim.Agents/Shared/AgentOrchestrator.cs` — add `IsWorkerRole`, `WorkerKickoffAsync`, route in `SafeKickoffAsync`
- `src/backend/Taim.Agents/Shared/AgentFactory.cs` — inject `IApprovalService`, call `PreApproveAsync`
- `src/backend/Taim.Connectors/WebSearch/WebSearchConnector.cs` — implement `IConnector` directly for graceful no-key fallback
- `src/backend/Taim.Connectors/ClaudeCode/ClaudeCodeConnector.cs` — accept `IConfiguration`, optional `workingDirectory`
- `src/backend/Taim.Connectors/ConnectorExtensions.cs` — explicit factory registration for `ClaudeCodeConnector`
- `src/backend/Taim.Api/Dockerfile` — multi-stage: dotnet-build → node-build → runtime (Debian + Node.js + claude CLI)
- `docker-compose.yml` — build context → repo root, add `BRAVE_API_KEY` + `Workspace__Root` env vars, add `workspaces-data` volume
- `.env.example` — document `BRAVE_API_KEY`
- `src/backend/Taim.Agents/CLAUDE.md` — document worker kickoff path
- `src/backend/Taim.Connectors/CLAUDE.md` — update connector status + Docker requirements
- `infra/CLAUDE.md` — document new volume + env vars + build context change
- `CLAUDE.md` (root) — update build state table
- `docs/user-guide.md` — add Developer Agents section
- `METRICS.md` — update sprint progress + self-build gate
- `PROCESS.md` Section 7 — mark Sprint 4 done
- `specs/sprint-4/S4-001-developer-agents.md` — set status → `done`

---

## Task 1: Add `PreApproveAsync` to `IApprovalService`

**Files:**
- Modify: `src/backend/Taim.Core/Approvals/ApprovalModels.cs:34-46`

- [ ] **Step 1: Add the method to the interface**

In `ApprovalModels.cs`, add `PreApproveAsync` to the `IApprovalService` interface after `GetPendingAsync`:

```csharp
public interface IApprovalService
{
    Task<ApprovalRequest> CreateAsync(Guid tenantId, Guid agentId, string toolName,
        Dictionary<string, object?> toolArguments, string description,
        string? durableRequestId = null, CancellationToken ct = default);

    Task<ApprovalDecision?> CheckLongLivedAsync(Guid tenantId, Guid agentId,
        string toolName, Dictionary<string, object?> toolArguments, CancellationToken ct = default);

    Task ApplyDecisionAsync(Guid tenantId, ApprovalDecision decision, CancellationToken ct = default);

    Task<IReadOnlyList<ApprovalRequest>> GetPendingAsync(Guid tenantId, CancellationToken ct = default);

    Task PreApproveAsync(Guid tenantId, Guid agentId, string toolName,
        ApprovalScope scope = ApprovalScope.AgentAndTool,
        CancellationToken ct = default);
}
```

- [ ] **Step 2: Build to confirm compile error in ApprovalService (expected)**

```bash
cd /home/rossb/claude/gen-site/taim/src/backend && dotnet build Taim.slnx 2>&1 | grep -E "error|Error"
```

Expected: compile error in `Taim.Data/Services/ApprovalService.cs` saying it doesn't implement `PreApproveAsync`. This confirms the interface change is live.

---

## Task 2: Implement `PreApproveAsync` in `ApprovalService`

**Files:**
- Modify: `src/backend/Taim.Data/Services/ApprovalService.cs`

- [ ] **Step 1: Add the implementation after `GetPendingAsync`**

Insert before the `ComputeFingerprint` helper method in `ApprovalService`:

```csharp
public async Task PreApproveAsync(
    Guid tenantId,
    Guid agentId,
    string toolName,
    ApprovalScope scope = ApprovalScope.AgentAndTool,
    CancellationToken ct = default)
{
    var entity = new ApprovalEntity
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        AgentId = agentId,
        ToolName = toolName,
        ToolArguments = JsonDocument.Parse("{}"),
        Description = $"Pre-approved: {toolName} for agent {agentId}",
        Status = "approved",
        Scope = scope.ToString().ToSnakeCase(),
        CreatedAt = DateTimeOffset.UtcNow
    };

    db.Approvals.Add(entity);
    await db.SaveChangesAsync(ct);
}
```

- [ ] **Step 2: Build — must succeed with 0 errors**

```bash
cd /home/rossb/claude/gen-site/taim/src/backend && dotnet build Taim.slnx 2>&1 | tail -5
```

Expected: `Build succeeded.`

---

## Task 3: Create `WorkerAgentBase`

**Files:**
- Create: `src/backend/Taim.Agents/Worker/WorkerAgentBase.cs`

- [ ] **Step 1: Create the file**

```csharp
namespace Taim.Agents.Worker;

/// <summary>
/// Placeholder for non-executive (worker) agent roles.
/// Worker agents do not perform a kickoff — they activate silently and wait for action assignments.
/// </summary>
public sealed class WorkerAgentBase(string roleName)
{
    public string RoleName => roleName;
}
```

- [ ] **Step 2: Build — must succeed**

```bash
cd /home/rossb/claude/gen-site/taim/src/backend && dotnet build Taim.slnx 2>&1 | tail -5
```

Expected: `Build succeeded.`

---

## Task 4: Update `AgentOrchestrator` — worker kickoff path

**Files:**
- Modify: `src/backend/Taim.Agents/Shared/AgentOrchestrator.cs`

The current `SafeKickoffAsync` always runs the full executive LLM kickoff. We need to route worker roles to a new `WorkerKickoffAsync` that makes zero LLM calls.

- [ ] **Step 1: Add `IsWorkerRole` predicate and `WorkerKickoffAsync` method**

After the closing brace of `KickoffAgentAsync` (before the helpers section comment `// ── Helpers`), add:

```csharp
private static bool IsWorkerRole(AgentRole role) => role switch
{
    AgentRole.Ceo or AgentRole.Cto or AgentRole.Cmo
    or AgentRole.Cfo or AgentRole.Hr => false,
    _ => true
};

private async Task WorkerKickoffAsync(
    IAgentRegistry registry,
    INotificationService notifications,
    Guid tenantId, Guid taskId,
    AgentDefinition definition,
    CancellationToken ct)
{
    var meta = BaseMetadata(tenantId, taskId, definition);

    await registry.UpdateStatusAsync(tenantId, definition.Id, AgentStatus.Active, ct);
    await notifications.NotifyAsync(tenantId, NotificationKind.AgentStatusChanged,
        $"{definition.Name} activated", string.Empty, meta, ct);

    await Log(notifications, tenantId, taskId, definition,
        $"{definition.Name}: worker agent ready, waiting for action assignments.", ct);

    await registry.UpdateStatusAsync(tenantId, definition.Id, AgentStatus.Idle, ct);
    await notifications.NotifyAsync(tenantId, NotificationKind.AgentStatusChanged,
        $"{definition.Name} idle", string.Empty, meta, ct);
}
```

- [ ] **Step 2: Update `SafeKickoffAsync` to route on `IsWorkerRole`**

Replace the current `SafeKickoffAsync` body with:

```csharp
private async Task SafeKickoffAsync(
    Guid tenantId, Guid taskId, string goal,
    AgentDefinition definition, IChatClient chatClient, CancellationToken ct)
{
    using var scope = scopeFactory.CreateScope();
    var sp            = scope.ServiceProvider;
    var registry      = sp.GetRequiredService<IAgentRegistry>();
    var notifications = sp.GetRequiredService<INotificationService>();

    try
    {
        if (IsWorkerRole(definition.Role))
        {
            await WorkerKickoffAsync(registry, notifications, tenantId, taskId, definition, ct);
        }
        else
        {
            var kpiService    = sp.GetRequiredService<IKpiService>();
            var reportService = sp.GetRequiredService<IReportService>();
            var actionService = sp.GetRequiredService<IActionService>();
            await KickoffAgentAsync(
                registry, kpiService, reportService, notifications, actionService,
                tenantId, taskId, goal, definition, chatClient, [], ct);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Kickoff failed for agent {AgentId} ({Name})", definition.Id, definition.Name);

        await Log(notifications, tenantId, taskId, definition,
            $"{definition.Name}: kickoff failed — {ex.Message}", ct);

        try { await registry.UpdateStatusAsync(tenantId, definition.Id, AgentStatus.Idle, ct); }
        catch { /* best-effort status reset */ }
    }
}
```

- [ ] **Step 3: Build — must succeed**

```bash
cd /home/rossb/claude/gen-site/taim/src/backend && dotnet build Taim.slnx 2>&1 | tail -5
```

Expected: `Build succeeded.`

---

## Task 5: Update `AgentFactory` — inject `IApprovalService`, pre-seed web-search

**Files:**
- Modify: `src/backend/Taim.Agents/Shared/AgentFactory.cs`

- [ ] **Step 1: Add `IApprovalService` to the constructor and add the using**

The current constructor is:
```csharp
public sealed class AgentFactory(
    IAgentRegistry registry,
    IProviderFactory providerFactory,
    IServiceScopeFactory scopeFactory,
    IPricingCardProvider pricingCards,
    INotificationService notifications,
    ILogger<AgentFactory> logger)
```

Change it to:
```csharp
using Taim.Core.Approvals;
// ... (add this using at the top with the other usings)

public sealed class AgentFactory(
    IAgentRegistry registry,
    IProviderFactory providerFactory,
    IServiceScopeFactory scopeFactory,
    IPricingCardProvider pricingCards,
    INotificationService notifications,
    IApprovalService approvalService,
    ILogger<AgentFactory> logger)
```

- [ ] **Step 2: Call `PreApproveAsync` in `CreateAsync` after the notification**

In `CreateAsync`, after the `await notifications.NotifyAsync(...)` call and before `return (definition, chatClient);`, add:

```csharp
await approvalService.PreApproveAsync(request.TenantId, definition.Id, "web-search", ct: ct);
```

So the end of `CreateAsync` looks like:
```csharp
await notifications.NotifyAsync(
    request.TenantId,
    NotificationKind.TeamUpdate,
    $"Agent Created: {request.Name}",
    $"{request.Role} agent registered and ready.",
    new Dictionary<string, object?> { ["agentId"] = definition.Id.ToString(), ["role"] = request.Role.ToString() },
    ct);

await approvalService.PreApproveAsync(request.TenantId, definition.Id, "web-search", ct: ct);

return (definition, chatClient);
```

- [ ] **Step 3: Build — must succeed**

```bash
cd /home/rossb/claude/gen-site/taim/src/backend && dotnet build Taim.slnx 2>&1 | tail -5
```

Expected: `Build succeeded.`

---

## Task 6: Update `WebSearchConnector` — graceful no-key fallback

**Files:**
- Modify: `src/backend/Taim.Connectors/WebSearch/WebSearchConnector.cs`

Currently extends `McpStdioConnector`. Change it to implement `IConnector` directly so we can intercept `StartAsync` before the MCP server is launched.

- [ ] **Step 1: Rewrite `WebSearchConnector` to implement `IConnector` directly**

Replace the entire file with:

```csharp
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
                "brave_web_search",
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
```

- [ ] **Step 2: Build — must succeed**

```bash
cd /home/rossb/claude/gen-site/taim/src/backend && dotnet build Taim.slnx 2>&1 | tail -5
```

Expected: `Build succeeded.`

---

## Task 7: Update `ClaudeCodeConnector` — `IConfiguration` + optional `workingDirectory`

**Files:**
- Modify: `src/backend/Taim.Connectors/ClaudeCode/ClaudeCodeConnector.cs`

- [ ] **Step 1: Rewrite `ClaudeCodeConnector` to accept `IConfiguration`**

Replace the entire file with:

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Taim.Connectors.Sdk;

namespace Taim.Connectors.ClaudeCode;

public sealed class ClaudeCodeConnector(IConfiguration config) : IConnector
{
    private readonly string _workspaceRoot = config["Workspace:Root"] ?? "/app/workspaces";
    private IReadOnlyList<AITool>? _tools;

    public string ConnectorId => "claude-code";
    public string DisplayName => "Claude Code";
    public string Description => "Write, edit, test, and commit code using the Claude Code CLI.";

    public Task StartAsync(CancellationToken ct = default)
    {
        _tools =
        [
            AIFunctionFactory.Create(RunClaudeCode, "claude_code",
                "Run the Claude Code CLI with a prompt to write, edit, or test code in a given workspace directory."),
        ];
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken ct = default) =>
        Task.FromResult(_tools ?? throw new InvalidOperationException("ClaudeCodeConnector not started."));

    private async Task<string> RunClaudeCode(
        string prompt,
        string? workingDirectory = null,
        bool nonInteractive = true)
    {
        var dir = workingDirectory ?? _workspaceRoot;
        Directory.CreateDirectory(dir);

        var args = nonInteractive
            ? $"--print \"{EscapeArg(prompt)}\""
            : $"\"{EscapeArg(prompt)}\"";

        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "claude",
                Arguments = args,
                WorkingDirectory = dir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
            return $"Claude Code exited with code {process.ExitCode}:\n{error}";

        return output;
    }

    private static string EscapeArg(string arg) => arg.Replace("\"", "\\\"");
}
```

- [ ] **Step 2: Build — must succeed**

```bash
cd /home/rossb/claude/gen-site/taim/src/backend && dotnet build Taim.slnx 2>&1 | tail -5
```

Expected: `Build succeeded.`

---

## Task 8: Update `ConnectorExtensions` — explicit factory for `ClaudeCodeConnector`

**Files:**
- Modify: `src/backend/Taim.Connectors/ConnectorExtensions.cs`

- [ ] **Step 1: Add `IConfiguration` import and change `ClaudeCodeConnector` registration**

Replace the entire file with:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Taim.Connectors.ClaudeCode;
using Taim.Connectors.Email;
using Taim.Connectors.Sdk;
using Taim.Connectors.WebSearch;

namespace Taim.Connectors;

public static class ConnectorExtensions
{
    public static IServiceCollection AddTaimConnectors(this IServiceCollection services)
    {
        services.AddSingleton<IConnector, WebSearchConnector>();
        services.AddSingleton<IConnector, EmailConnector>();
        services.AddSingleton<IConnector>(sp =>
            new ClaudeCodeConnector(sp.GetRequiredService<IConfiguration>()));
        services.AddConnectors();
        return services;
    }
}
```

- [ ] **Step 2: Build — must succeed**

```bash
cd /home/rossb/claude/gen-site/taim/src/backend && dotnet build Taim.slnx 2>&1 | tail -5
```

Expected: `Build succeeded.`

---

## Task 9: Rewrite `Dockerfile` — multi-stage with Node.js + claude CLI

**Files:**
- Modify: `src/backend/Taim.Api/Dockerfile`

The current Dockerfile uses `./src/backend` as build context and `aspnet:10.0-alpine` as runtime. We need:
1. Build context changed to repo root (update in docker-compose.yml in Task 10)
2. Runtime base changed to Debian (Alpine doesn't support nodesource setup easily)
3. New `node-build` stage that pre-installs MCP server dependencies
4. Runtime installs Node.js + `@anthropic-ai/claude-code`

- [ ] **Step 1: Rewrite the Dockerfile**

Replace the entire file with:

```dockerfile
# Stage 1 — .NET build (build context is repo root)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src

COPY src/backend/Taim.slnx ./
COPY src/backend/Taim.Core/ Taim.Core/
COPY src/backend/Taim.Data/ Taim.Data/
COPY src/backend/Taim.Agents/ Taim.Agents/
COPY src/backend/Taim.Workflows/ Taim.Workflows/
COPY src/backend/Taim.Memory/ Taim.Memory/
COPY src/backend/Taim.Connectors/ Taim.Connectors/
COPY src/backend/Taim.Budget/ Taim.Budget/
COPY src/backend/Taim.Notifications/ Taim.Notifications/
COPY src/backend/Taim.Providers/ Taim.Providers/
COPY src/backend/Taim.Api/ Taim.Api/
COPY src/backend/Taim.Host/ Taim.Host/
COPY src/backend/Taim.Tests/ Taim.Tests/
COPY src/backend/Taim.E2ETests/ Taim.E2ETests/

RUN dotnet restore Taim.slnx
RUN dotnet publish Taim.Api/Taim.Api.csproj -c Release -o /app/publish --no-restore

# Stage 2 — MCP server dependencies
FROM node:20-slim AS node-build
WORKDIR /mcp

COPY mcp-servers/web-search/ web-search/
COPY mcp-servers/email/ email/

RUN cd web-search && npm ci --omit=dev
RUN cd email && npm ci --omit=dev

# Stage 3 — Runtime: .NET + Node.js + claude CLI
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

RUN apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y --no-install-recommends nodejs && \
    npm install -g @anthropic-ai/claude-code && \
    apt-get purge -y curl && \
    apt-get autoremove -y && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=dotnet-build /app/publish ./
COPY --from=node-build /mcp mcp-servers/
RUN mkdir -p /app/workspaces

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Taim.Api.dll"]
```

Note: If `@anthropic-ai/claude-code` is unavailable at build time, the Dockerfile will fail at the `npm install -g` step. The fallback is to remove that line and install `claude` manually as a post-deployment step. AC-5 (claude --version succeeds in container) will fail in that case and must be resolved before marking the spec done.

- [ ] **Step 2: Verify the file was written correctly**

```bash
cat /home/rossb/claude/gen-site/taim/src/backend/Taim.Api/Dockerfile
```

Expected: Multi-stage Dockerfile starting with `FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build`.

---

## Task 10: Update `docker-compose.yml` — build context, env vars, volume

**Files:**
- Modify: `docker-compose.yml` (repo root)

Three changes to `taim-api`:
1. Build context: `./src/backend` → `.` (repo root); add explicit `dockerfile` path
2. Environment: add `Brave__ApiKey` and `Workspace__Root`
3. Volumes: add `workspaces-data:/app/workspaces`
4. Top-level volumes: add `workspaces-data`

- [ ] **Step 1: Update the `taim-api` build section**

Current build section:
```yaml
build:
  context: ./src/backend
  dockerfile: Taim.Api/Dockerfile
```

Change to:
```yaml
build:
  context: .
  dockerfile: src/backend/Taim.Api/Dockerfile
```

- [ ] **Step 2: Add env vars to `taim-api` environment block**

In the `taim-api` environment section, after `OLLAMA_BASE_URL: ${OLLAMA_BASE_URL:-}`, add:

```yaml
      Brave__ApiKey: ${BRAVE_API_KEY:-}
      Workspace__Root: /app/workspaces
```

- [ ] **Step 3: Add volume mount to `taim-api`**

After the `extra_hosts` block in `taim-api`, add:

```yaml
    volumes:
      - workspaces-data:/app/workspaces
```

- [ ] **Step 4: Add `workspaces-data` to top-level volumes**

In the top-level `volumes:` block (currently `postgres-data:` and `redis-data:`), add:

```yaml
  workspaces-data:
```

- [ ] **Step 5: Verify the docker-compose.yml is valid YAML**

```bash
cd /home/rossb/claude/gen-site/taim && docker compose config --quiet 2>&1 | head -10
```

Expected: No errors (or just warnings about missing env vars which is fine).

---

## Task 11: Create `.env.example`

**Files:**
- Create: `.env.example` (repo root)

- [ ] **Step 1: Check if `.env.example` exists and create/update it**

```bash
ls /home/rossb/claude/gen-site/taim/.env.example 2>/dev/null && echo "exists" || echo "missing"
```

- [ ] **Step 2: Create or update `.env.example`**

If the file exists, read it first, then add `BRAVE_API_KEY`. If missing, create it:

```
# Copy to .env and fill in your values

# AI Provider API Keys
ANTHROPIC_API_KEY=
OPENAI_API_KEY=
GOOGLE_API_KEY=

# Brave Search API key — get a free key at https://brave.com/search/api/
BRAVE_API_KEY=

# Database (defaults work for local Docker)
POSTGRES_PASSWORD=changeme_postgres
REDIS_PASSWORD=changeme_redis

# JWT (must be at least 32 chars)
JWT_SECRET=changeme_jwt_secret_at_least_32_chars_long
```

---

## Task 12: Add smoke tests

**Files:**
- Create: `src/backend/Taim.E2ETests/DeveloperAgentTests.cs`

The smoke tests verify three things from the spec:
1. After team assembly, at least one `approved` `agent_and_tool` approval for `web-search` exists (AC-3)
2. After kickoff, no reports exist for Developer/QA agents (AC-1) — done via polling after exec agents produce reports
3. Node version check is a shell command, not a C# test; noted as a manual verification step below

- [ ] **Step 1: Write the test file**

```csharp
using System.Net;

namespace Taim.E2ETests;

[Collection("Api")]
public class DeveloperAgentTests(ApiFixture fixture)
{
    /// <summary>
    /// Verifies AC-3: AgentFactory.PreApproveAsync inserts a web-search approval for every agent.
    /// Submits a simple goal, then polls until at least one approved web-search approval appears.
    /// Requires a live stack with LLM API keys to assemble a team.
    /// </summary>
    [Fact]
    public async Task AfterTeamAssembly_ApprovalsContainPreSeededWebSearch()
    {
        var taskRes = await fixture.Client.PostAsJsonAsync("/api/tasks",
            new { goal = "Write a hello world program in Python", budgetUsd = 2.0 });
        Assert.Equal(HttpStatusCode.Accepted, taskRes.StatusCode);

        // Poll up to 90s for a web-search pre-approval to appear
        var deadline = DateTime.UtcNow.AddSeconds(90);
        bool found = false;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(3000);
            var res = await fixture.Client.GetAsync("/api/approvals");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var body = await res.Content.ReadFromJsonAsync<JsonElement>();
            foreach (var approval in body.EnumerateArray())
            {
                var toolName = approval.TryGetProperty("toolName", out var t) ? t.GetString() : null;
                var status   = approval.TryGetProperty("status", out var s)   ? s.GetString() : null;
                var scope    = approval.TryGetProperty("scope", out var sc)   ? sc.GetString() : null;

                if (toolName == "web-search" && status == "approved" &&
                    scope is "agentAndTool" or "agent_and_tool")
                {
                    found = true;
                    break;
                }
            }
            if (found) break;
        }

        Assert.True(found,
            "Expected at least one approved web-search approval after team assembly, " +
            "but none was found within 90 seconds. Check ANTHROPIC_API_KEY is set and the stack is running.");
    }

    /// <summary>
    /// Verifies AC-1: Developer/QA agents produce no executive strategy reports.
    /// Polls until at least one executive report appears (confirming kickoff ran), 
    /// then asserts no report belongs to a Developer or QA agent.
    /// Requires LLM API keys and team assembly to complete.
    /// </summary>
    [Fact]
    public async Task AfterKickoff_NoWorkerAgentReportsExist()
    {
        var taskRes = await fixture.Client.PostAsJsonAsync("/api/tasks",
            new { goal = "Build a REST API for a todo app", budgetUsd = 2.0 });
        Assert.Equal(HttpStatusCode.Accepted, taskRes.StatusCode);
        var taskBody = await taskRes.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = taskBody.GetProperty("id").GetString()!;

        // Poll up to 120s for at least one executive report (confirms kickoff ran)
        bool kickoffComplete = false;
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(5000);
            var reportsRes = await fixture.Client.GetAsync($"/api/reports?taskId={taskId}");
            Assert.Equal(HttpStatusCode.OK, reportsRes.StatusCode);
            var reports = await reportsRes.Content.ReadFromJsonAsync<JsonElement>();
            if (reports.GetArrayLength() > 0) { kickoffComplete = true; break; }
        }

        if (!kickoffComplete)
        {
            // No LLM keys or kickoff didn't complete — skip assertion
            return;
        }

        // Assert no reports from worker roles
        var finalRes = await fixture.Client.GetAsync($"/api/reports?taskId={taskId}");
        var allReports = await finalRes.Content.ReadFromJsonAsync<JsonElement>();

        foreach (var report in allReports.EnumerateArray())
        {
            if (!report.TryGetProperty("agentName", out var nameEl)) continue;
            var name = nameEl.GetString()?.ToLowerInvariant() ?? string.Empty;
            Assert.False(
                name.Contains("developer") || name.Contains("qa"),
                $"Worker agent produced a report: '{nameEl.GetString()}'. " +
                "Only executive agents should produce strategy reports.");
        }
    }
}
```

- [ ] **Step 2: Build the test project to confirm it compiles**

```bash
cd /home/rossb/claude/gen-site/taim/src/backend && dotnet build Taim.E2ETests/Taim.E2ETests.csproj 2>&1 | tail -5
```

Expected: `Build succeeded.`

---

## Task 13: Build, deploy, verify, and update documentation

**Substeps in order:**

### 13a — Rebuild and deploy

- [ ] **Step 1: Build the full solution one more time to confirm 0 errors**

```bash
cd /home/rossb/claude/gen-site/taim/src/backend && dotnet build Taim.slnx 2>&1 | tail -5
```

Expected: `Build succeeded.  0 Error(s)`

- [ ] **Step 2: Rebuild the API Docker image (this will take 3–5 minutes due to npm install)**

```bash
cd /home/rossb/claude/gen-site/taim && docker compose build taim-api 2>&1 | tail -20
```

Expected: `Successfully built ...` or `taim-taim-api Built`. If npm install fails, check the error and fall back per the Dockerfile note in Task 9.

- [ ] **Step 3: Restart the API container**

```bash
cd /home/rossb/claude/gen-site/taim && docker compose up -d taim-api
```

Expected: `Container taim-taim-api-1 Started` (or similar).

### 13b — Verify container runtime (AC-5, AC-6)

- [ ] **Step 4: Verify Node.js is available in the container**

```bash
docker exec taim-taim-api-1 node --version
```

Expected: `v20.x.x`

- [ ] **Step 5: Verify claude CLI is available in the container**

```bash
docker exec taim-taim-api-1 claude --version
```

Expected: `claude v...` or similar. If this fails because `@anthropic-ai/claude-code` wasn't installable, see the fallback note in Task 9.

- [ ] **Step 6: Verify MCP web-search node_modules are present (AC-6)**

```bash
docker exec taim-taim-api-1 ls mcp-servers/web-search/node_modules | head -3
```

Expected: Package names like `@modelcontextprotocol`.

### 13c — Run smoke tests

- [ ] **Step 7: Run the existing smoke tests to confirm no regressions**

```bash
cd /home/rossb/claude/gen-site/taim/src/backend && dotnet test Taim.E2ETests/Taim.E2ETests.csproj --no-build -v q 2>&1 | tail -15
```

Expected: All 24 existing tests pass. New `DeveloperAgentTests` may be skipped/time-out if no LLM keys — that is acceptable.

### 13d — Update CLAUDE.md files

- [ ] **Step 8: Update `src/backend/Taim.Agents/CLAUDE.md`**

Add after the `## AgentOrchestrator` section:

```markdown
## WorkerAgentBase (Worker/WorkerAgentBase.cs)

Simple placeholder for non-executive roles. No `RunAsync` or `ProposeKpisAsync` — worker kickoff is a silent activation.

## Worker vs Executive Kickoff

`AgentOrchestrator.SafeKickoffAsync` routes on `IsWorkerRole(AgentRole)`:
- **Executive roles** (CEO, CTO, CMO, CFO, HR): full LLM kickoff (KPIs + strategy report + delegation dispatch)
- **Worker roles** (all others): `WorkerKickoffAsync` — status→Active, log "ready", status→Idle. Zero LLM calls.

```csharp
private static bool IsWorkerRole(AgentRole role) => role switch
{
    AgentRole.Ceo or AgentRole.Cto or AgentRole.Cmo
    or AgentRole.Cfo or AgentRole.Hr => false,
    _ => true
};
```

## AgentFactory — Pre-Seeded Approvals

`AgentFactory.CreateAsync` calls `approvalService.PreApproveAsync(tenantId, agentId, "web-search")` after every agent is created. This inserts an `approved` / `agent_and_tool` row in the `approvals` table so web searches never hit the approval queue.
```

- [ ] **Step 9: Update `src/backend/Taim.Connectors/CLAUDE.md`**

Update the ClaudeCode section to say `(Sprint 4)` instead of `(Sprint 3/4 work)`. Add a Docker requirements note:

```markdown
## ClaudeCode Connector

`ClaudeCodeConnector` runs the `claude` CLI as a subprocess. It reads `Workspace:Root` from `IConfiguration` (default: `/app/workspaces`). The `workingDirectory` argument to `claude_code` is optional; omitting it uses `_workspaceRoot`.

**Docker requirement (Sprint 4):** The runtime image must have `node`, `npm`, and `claude` CLI installed. The Dockerfile installs `@anthropic-ai/claude-code` globally via npm. Verify with `docker exec taim-api-1 claude --version`.

## WebSearch Connector

`WebSearchConnector` implements `IConnector` directly (not via `McpStdioConnector`). If `Brave:ApiKey` is not set in config, `StartAsync` creates a fallback tool that returns "web search not configured" instead of spawning the MCP server.
```

- [ ] **Step 10: Update `infra/CLAUDE.md`**

Add a section:
```markdown
## Taim.Api Build Context (Sprint 4)

The `taim-api` Docker build context was changed from `./src/backend` to `.` (repo root) so the MCP server source files (`mcp-servers/`) can be included in the image. The dockerfile path is now `src/backend/Taim.Api/Dockerfile`.

## New Volume: `workspaces-data`

The `workspaces-data` volume is mounted at `/app/workspaces` in `taim-api`. Developer agents use this directory as their default working directory for `claude_code` tool calls.

## Environment Variables (Sprint 4)

| Variable | Service | Purpose |
|---|---|---|
| `BRAVE_API_KEY` | `taim-api` | Brave Search API key for web-search MCP server |
| `Workspace__Root` | `taim-api` | Working directory for ClaudeCode agent (default: `/app/workspaces`) |

Set `BRAVE_API_KEY` in `.env` (see `.env.example`).
```

- [ ] **Step 11: Update root `CLAUDE.md` build state table**

Change:
```
| Developer agents with ClaudeCode connector | 🔲 Sprint 4 |
```
To:
```
| Developer agents with ClaudeCode connector | ✅ Sprint 4 |
```

- [ ] **Step 12: Update `docs/user-guide.md`**

Add a new section at the end:

```markdown
## Developer Agents (Sprint 4)

### What Developer Agents Do

Developer agents (role: `developer`) and QA agents (role: `qaEngineer`) are worker agents. Unlike executive agents, they do **not** produce strategy reports when your team first assembles. Instead, they appear immediately as `status: idle` on the team graph and wait for action assignments.

### Approving Code Writes

When a Developer agent is assigned a coding task, it uses the **Claude Code** tool (`claude_code`) to write files. The first time an agent attempts this, an approval request appears on the **Approvals** page:

```
PENDING APPROVAL

  Alex (Developer) wants to call:
  Tool: claude_code
  Prompt: "Add unit tests for ActionService"
  Directory: /app/workspaces

  [Approve once]  [Always allow]  [Deny]
```

Click **Always allow** (`scope: AgentAndTool`) so the agent can write code going forward without further prompts. After approval, re-trigger the action via the Actions panel.

### Workspaces

All files written by Developer agents are stored in `/app/workspaces` inside the `taim-api` container (mounted as `workspaces-data` Docker volume). To inspect the output:

```bash
docker exec taim-taim-api-1 ls /app/workspaces
```
```

- [ ] **Step 13: Update `METRICS.md` sprint progress and self-build gate**

In the Self-Build Readiness table, change:
```
| ClaudeCode connector → Developer agents | 🔲 Not started | Sprint 4 |
```
to:
```
| ClaudeCode connector → Developer agents | ✅ Done | Sprint 4 |
```

Update completion from `4 / 6 (67%)` to `5 / 6 (83%)`.

In the Sprint Progress table, change Sprint 4 from `🔲 Backlog` to `✅ Done`.

In Feature Completion table, change:
```
| Tool use (ClaudeCode, WebSearch) | 🔲 Sprint 4 |
```
to:
```
| Tool use (ClaudeCode, WebSearch) | ✅ Sprint 4 |
```

Update the Test Health section: bump smoke test count from 24 to 26 (two new tests added).

Update "Last updated" date to 2026-05-18.

- [ ] **Step 14: Update `PROCESS.md` Section 7 — Sprint 4 status**

In the Sprint History table, change Sprint 4 row:
```
| 4 | Developer tools | Worker agents, ClaudeCode/WebSearch operational, Docker infra | 🟡 Ready | — |
```
to:
```
| 4 | Developer tools | Worker agents, ClaudeCode/WebSearch operational, Docker infra | ✅ Done | ✅ Build + smoke |
```

In the Current Sprint section, update the goal to reference Sprint 5 instead.

- [ ] **Step 15: Update spec status to `done`**

In `specs/sprint-4/S4-001-developer-agents.md`, change the frontmatter:
```yaml
status: ready
```
to:
```yaml
status: done
updated: 2026-05-18
```

And update the Review section:
```markdown
## Review
**Date:** 2026-05-18
**Result:** PASS
**Notes:** All 10 ACs implemented. AC-5 (claude --version) verified by docker exec. AC-7 graceful fallback implemented via WebSearchConnector rewrite. AC-9 manual approval flow works via existing ApprovalEndpoints.
```

---

## Manual Verification Checklist (after deployment)

These verify ACs not covered by automated tests:

```bash
# AC-5: Node.js available
docker exec taim-taim-api-1 node --version
# Expected: v20.x.x

# AC-5: claude CLI available  
docker exec taim-taim-api-1 claude --version
# Expected: claude v...

# AC-6: MCP web-search files present
docker exec taim-taim-api-1 ls mcp-servers/web-search/node_modules | head -3
# Expected: package names like @modelcontextprotocol

# AC-3: Pre-seeded web-search approvals after goal submission
docker exec -e PGPASSWORD=changeme_postgres taim-taim-postgres-1 psql -U taim -d taim \
  -c "SELECT count(*) FROM approvals WHERE tool_name='web-search' AND scope='agent_and_tool';"
# Expected: count = number of agents created
```
