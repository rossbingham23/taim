# Taim.Tests — Unit & Integration Tests

HTTP smoke tests against the live stack (requires Docker services running).

## Running

```bash
# From src/backend/
dotnet test Taim.Tests/Taim.Tests.csproj
```

Requires: `http://localhost:5000` live (run `./start.sh` first).

## Test Files

| File | Covers |
|---|---|
| `AuthTests.cs` | POST /api/auth/login, register |
| `AgentTests.cs` | GET /api/agents, GET /api/agents/{id} |
| `ApprovalTests.cs` | GET /api/approvals, POST /api/approvals/{id}/decide |
| `KpiTests.cs` | GET /api/kpis, POST /api/kpis/{id}/values |
| `TaskTests.cs` | POST /api/tasks, GET /api/tasks, GET /api/tasks/{id} |
| `HealthTests.cs` | GET /health |
| `ApiFixture.cs` | Shared `HttpClient` + auth token setup |

## Conventions

- Each test file uses `ApiFixture` to get an authenticated `HttpClient`
- Tests are smoke tests — they verify HTTP 200/201/204, not deep business logic
- No database mocking — all tests hit the real PostgreSQL via Docker

## Adding a New Test

Add a test class referencing `ApiFixture`. Follow the pattern in `TaskTests.cs`:
- `IClassFixture<ApiFixture>` for the class
- `_client` from fixture for all requests
- Assert status codes + minimal JSON shape checks
