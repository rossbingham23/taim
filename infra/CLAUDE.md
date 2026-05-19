# TAIM Infrastructure

Docker Compose stack. All services defined in `docker-compose.yml` at the repo root.

## Services

| Service | Image | Port | Purpose |
|---|---|---|---|
| `taim-api` | Custom (Dockerfile in `src/backend/Taim.Api/`) | 5000 | ASP.NET Core API |
| `taim-web` | Custom (Dockerfile in `src/frontend/taim-web/`) | 3000 | React SPA + nginx proxy |
| `taim-postgres` | `pgvector/pgvector:pg16` | 5432 | PostgreSQL 16 |
| `taim-redis` | `redis:7-alpine` | 6379 | Redis cache |

## Database Schema

`postgres/init.sql` — single source of truth for all table definitions and RLS policies.

**No EF migrations** — schema is managed manually. To apply schema changes:
1. Update `init.sql`
2. Run the ALTER/CREATE TABLE directly against the running container (for dev)
3. Rebuild the volume for a clean state: `docker compose down -v && ./start.sh`

### Running SQL on the live container

```bash
docker exec -e PGPASSWORD=taim taim-postgres-1 psql -U taim -d taim -c "YOUR SQL HERE"
```

## Nginx (`nginx.conf`)

Serves the React SPA from `dist/` and proxies:
- `/api/*` → `http://taim-api:8080`
- `/hubs/*` → `http://taim-api:8080` (WebSocket upgrade for SignalR)

## Common Operations

```bash
./start.sh                              # Full rebuild + start
docker compose up -d                    # Start in background (no rebuild)
docker compose build taim-api           # Rebuild API only
docker compose logs -f taim-api         # Tail API logs
docker compose down                     # Stop all services (data preserved)
docker compose down -v                  # Stop + destroy volumes (resets DB)
```

## Environment / Secrets

`docker-compose.yml` injects env vars from `.env` if present. Key variables:
- `ANTHROPIC_API_KEY` — required for Anthropic provider
- `OPENAI_API_KEY` — optional, for OpenAI provider
- `JWT_SECRET` — JWT signing secret
- `POSTGRES_PASSWORD` — defaults to `taim`

## Redis

Used for distributed caching (approvals, session data). Connection string configured in `appsettings.json`.

## Sprint 4 Infrastructure Changes

### Taim.Api Build Context

The `taim-api` Docker build context is now `.` (repo root) so `mcp-servers/` files can be included. Dockerfile path: `src/backend/Taim.Api/Dockerfile`.

### New Volume: `workspaces-data`

Mounted at `/app/workspaces` in `taim-api`. Developer agents use this as their default working directory for `claude_code` tool calls.

### New Environment Variables

| Variable | Service | Purpose |
|---|---|---|
| `BRAVE_API_KEY` | `taim-api` | Brave Search API key (set in `.env`) |
| `Workspace__Root` | `taim-api` | Working directory for ClaudeCode connector |

### Node.js + claude CLI

The runtime image (`mcr.microsoft.com/dotnet/aspnet:10.0-alpine`) has Node.js and `@anthropic-ai/claude-code` installed. MCP server npm dependencies are pre-built in the image via the `node-build` stage.
