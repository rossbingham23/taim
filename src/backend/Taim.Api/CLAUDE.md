# Taim.Api — HTTP API

ASP.NET Core Minimal APIs. Entry point: `Program.cs`.

## Endpoint Groups

| Route prefix | File | Description |
|---|---|---|
| `POST /api/auth/login` | `AuthEndpoints.cs` | JWT login |
| `POST /api/auth/register` | `AuthEndpoints.cs` | Tenant + user creation |
| `GET/POST /api/tasks` | `TaskEndpoints.cs` | Submit goal, list tasks, get task+graph |
| `GET /api/tasks/{taskId}` | `TaskEndpoints.cs` | Get task + team graph |
| `GET /api/agents` | `AgentEndpoints.cs` | List all tenant agents |
| `GET /api/agents/{agentId}` | `AgentEndpoints.cs` | Agent detail + KPIs + direct reports |
| `GET /api/kpis?taskId=` | `KpiEndpoints.cs` | KPI hierarchy roots for a task |
| `POST /api/kpis/{kpiId}/values` | `KpiEndpoints.cs` | Record a KPI measurement |
| `GET /api/approvals` | `ApprovalEndpoints.cs` | Pending approvals |
| `POST /api/approvals/{id}/decide` | `ApprovalEndpoints.cs` | Approve or deny |
| `GET /api/reports?taskId=` | `ReportEndpoints.cs` | Executive reports for a task |
| `GET /api/actions?taskId=` | `ActionEndpoints.cs` | List work-item actions for a task |
| `POST /api/actions` | `ActionEndpoints.cs` | Create an action |
| `PATCH /api/actions/{id}` | `ActionEndpoints.cs` | Update action status/assignment |
| `GET /api/meetings?taskId=` | `MeetingEndpoints.cs` | List meetings for a task |
| `GET /api/meetings/{id}` | `MeetingEndpoints.cs` | Get meeting detail + full transcript |
| `GET /health` | `Program.cs` | Health check |
| `/hubs/agents` | `AgentEventHub.cs` | SignalR hub |

## Authentication

- JWT Bearer tokens; secret from `Jwt:Secret` in config / env
- `TenantMiddleware` extracts `tenantId` from the JWT `tenantId` claim and sets `TenantIdAccessor`
- All `/api/*` routes (except `/api/auth/*`) require `[Authorize]`
- SignalR clients pass token via `?access_token=` query string (configured in `Program.cs`)
- Background tasks extract `tenantId` from the task context, not middleware

## JSON Serialization

```csharp
// HTTP endpoints — camelCase enum strings
builder.Services.Configure<JsonOptions>(o =>
    o.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

// SignalR — snake_case enum strings (NotificationKind)
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
        options.PayloadSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)));
```

## SignalR Hub (AgentEventHub)

- Clients join group `tenant:{tenantId}` on connect
- `AgentHubNotifier` implements `INotificationChannel` and broadcasts via `IHubContext<AgentEventHub>`
- `NotificationService` (singleton) iterates all `INotificationChannel` implementations

## Error Patterns

- `401` — missing/invalid JWT or unresolvable `tenantId`
- `400` — missing required query params (taskId, etc.)
- `404` — resource not found (explicit `Results.NotFound()`)
- `500` — unhandled exception (logs full exception via ILogger)

## Adding a New Endpoint Group

1. Create `Endpoints/XxxEndpoints.cs` with `MapXxxEndpoints(this IEndpointRouteBuilder app)`
2. Add `app.MapXxxEndpoints();` in `Program.cs`
3. Add `services.AddScoped<IXxxService, XxxService>();` in the appropriate extensions file
