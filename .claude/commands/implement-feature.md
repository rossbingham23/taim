Implement a feature from its spec. Usage: /implement-feature specs/sprint-N/SN-NNN-feature-name.md

## Step 1: Load Context

Read these files IN ORDER before writing any code:
1. `CLAUDE.md` (already loaded)
2. `PROCESS.md` — invariants and process rules
3. `METRICS.md` — current state, open bugs
4. The spec file passed as argument
5. CLAUDE.md files for every module you will touch (look at the spec's "Implementation Order" section)

Do not skip this step. The spec tells you WHAT to build. The CLAUDE.md files tell you HOW the system works.

## Step 2: Verify Understanding

Before writing any code, confirm:
- [ ] You understand every acceptance criterion in the spec
- [ ] You know which files to create/modify (spec's "Implementation Order")
- [ ] You understand all DB schema changes needed
- [ ] You know which invariants apply (from PROCESS.md Section 3, Stage 3)

## Step 3: Implement (one AC at a time)

For each acceptance criterion in the spec:
1. Write the code
2. Run `cd src/backend && dotnet build Taim.slnx` — must succeed (0 errors)
3. Check the box: `- [x] AC-N: ...`

Work top-to-bottom through the implementation order:
- Core interfaces first (Taim.Core)
- Data layer second (Taim.Data entities, EF config, service)
- API endpoints third (Taim.Api)
- Agent logic fourth (Taim.Agents)
- Frontend last

## Step 4: Schema Changes

If the spec adds a new table:
1. Add it to `infra/postgres/init.sql` (before the RLS block, in the RLS tables array, in the ALTER TABLE RLS block)
2. Run on the live container:
   ```bash
   docker exec -e PGPASSWORD=taim taim-postgres-1 psql -U taim -d taim -c "CREATE TABLE IF NOT EXISTS ..."
   ```

## Step 5: Post-Implementation

After all ACs are checked:

1. Set spec status → `done` in the spec frontmatter
2. Update the spec's `updated` date
3. Update all touched module `CLAUDE.md` files (tables, interfaces, endpoints)
4. Update root `CLAUDE.md` build state table
5. Run smoke tests: `cd src/backend && dotnet test Taim.Tests/Taim.Tests.csproj`
6. Rebuild API: `docker compose build taim-api && docker compose up -d taim-api`
7. If frontend changed: `docker compose build taim-web && docker compose up -d taim-web`
8. Update `METRICS.md` sprint progress

## Invariants (Never Violate)

- Scoped DbContext — never AddDbContextPool
- `.HasColumnName()` for EVERY EF property
- No `ChatResponseFormat.Json` for Anthropic
- Background work uses `IServiceScopeFactory` — never inject Scoped services directly
- Executive agents: `new CeoAgent(client)` — never resolve from DI
- RLS is automatic — no manual WHERE tenant_id in queries
- `AgentJson.Deserialize<T>()` for all LLM JSON parsing
