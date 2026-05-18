# TAIM Backend — .NET 10 Solution

Solution file: `Taim.slnx` (new `.slnx` format, not `.sln`).

## Build & Test

```bash
dotnet build Taim.slnx
dotnet test Taim.E2ETests/Taim.E2ETests.csproj   # requires live Docker stack
dotnet test Taim.Tests/Taim.Tests.csproj          # unit tests (no Docker needed)
```

## Project Dependency Graph

```
Taim.Core          (no dependencies — pure domain)
├── Taim.Data      (EF Core + PostgreSQL + Redis)
├── Taim.Budget    (IChatClient middleware for spend tracking)
├── Taim.Agents    (Bootstrap, Expert, Executive agents; AgentOrchestrator)
│   └── uses Taim.Budget
├── Taim.Memory    (KpiContextProvider, TeamContextProvider, ChatHistoryProvider)
│   └── uses Taim.Data
├── Taim.Providers (IProviderFactory → Anthropic/OpenAI/Google/Ollama IChatClient)
├── Taim.Connectors (McpStdioConnector — spawns MCP servers as subprocesses)
├── Taim.Workflows (Durable task scaffolding — future use)
├── Taim.Notifications (NotificationService → INotificationChannel broadcast)
└── Taim.Api       (Minimal API host — references everything above)
```

## DI Rules

| Service | Lifetime | Reason |
|---|---|---|
| `TaimDbContext` | Scoped | `RlsInterceptor` is Scoped and injected into DbContext options |
| `RlsInterceptor` | Scoped | Sets `app.tenant_id` per-request |
| `TenantIdAccessor` | Scoped | Holds resolved `tenantId` for the current scope |
| `IProviderFactory` | Scoped | Reads per-tenant provider config from DB |
| `AgentFactory` | Scoped | Creates agents with budget-wrapped IChatClient |
| `AgentOrchestrator` | Scoped | Runs agent kickoff within a scope |
| Background tasks | Use `IServiceScopeFactory` | `Task.Run` blocks must create their own scope |
| Executive agents (`CeoAgent`, etc.) | **NOT in DI** | Instantiated directly with per-agent `IChatClient` |

## Known Constraints

- **EF 10.0.8**: Taim.Agents and Taim.Memory must pin `<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.8" />` to avoid version conflicts with Taim.Data.
- **pgvector**: max version `0.3.0` for `Pgvector.EntityFrameworkCore`.
- **Anthropic + response format**: Do NOT use `ChatResponseFormat.Json` (`response_format.type` must be `json_schema`, not `json_object`). Use explicit JSON structure in the system prompt.
- **LLM JSON parsing**: always use `AgentJson.Deserialize<T>()` (in `Taim.Agents/Shared/AgentJson.cs`). Strips markdown fences, normalizes snake_case keys to camelCase, then deserializes.
- **Enum serialization**: `AgentRole`, `AgentStatus` etc. stored as lowercase strings in DB via `.ToString().ToLowerInvariant()`. Served over HTTP as camelCase strings via `JsonStringEnumConverter(CamelCase)`.

## Entry Points

- `Taim.Api/Program.cs` — HTTP host (ports 8080 internal / 5000 exposed)
- `Taim.Host/Program.cs` — Worker host for background/durable workflows (port 5001)
- `Taim.E2ETests/` — Playwright tests, require `http://localhost:3000` live
