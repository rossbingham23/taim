Add a new API endpoint group to TAIM.

## Steps

1. **Define the interface** in `Taim.Core/<Domain>/<Domain>Models.cs`:
   - `sealed record` for each DTO (request/response)
   - `interface IXxxService` with async methods returning records

2. **Add the entity** to `Taim.Data/Models/Entities.cs`:
   - `class XxxEntity` with `{ get; set; }` properties
   - String status fields with defaults
   - Nullable Guids for optional FK references

3. **Configure EF** in `Taim.Data/TaimDbContext.cs`:
   - Add `DbSet<XxxEntity> Xxxs => Set<XxxEntity>();`
   - Add `modelBuilder.Entity<XxxEntity>(e => { e.ToTable("xxx"); e.Property(...).HasColumnName("..."); ... })` for EVERY property

4. **Update the schema**:
   - Add the table to `infra/postgres/init.sql` (before the RLS block)
   - Add the table name to the RLS tables array in `init.sql`
   - Add `ALTER TABLE xxx ENABLE ROW LEVEL SECURITY;` to the RLS block
   - Run the DDL on the live container:
     ```bash
     docker exec -e PGPASSWORD=taim taim-postgres-1 psql -U taim -d taim -c "CREATE TABLE IF NOT EXISTS ..."
     ```

5. **Implement the service** in `Taim.Data/Services/XxxService.cs`:
   - Constructor: `public sealed class XxxService(TaimDbContext db) : IXxxService`
   - Use `db.Xxxs.AsNoTracking()` for reads
   - Use `db.SaveChangesAsync(ct)` for writes
   - Map entity → record with a private static `ToRecord(XxxEntity e)` method

6. **Register** in `Taim.Data/DataExtensions.cs`:
   - Add `services.AddScoped<IXxxService, XxxService>();`
   - Add `using Taim.Core.<Domain>;`

7. **Create the endpoint** in `Taim.Api/Endpoints/XxxEndpoints.cs`:
   - Pattern: `app.MapGroup("/api/xxx").RequireAuthorization().WithTags("Xxx")`
   - Extract tenantId: `Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId)`
   - Return `Results.Ok(...)`, `Results.Created(...)`, `Results.NotFound()`, `Results.Unauthorized()`

8. **Wire in Program.cs**:
   - Add `app.MapXxxEndpoints();`

9. **Update CLAUDE.md files**:
   - `Taim.Core/CLAUDE.md` — add interface to Interfaces table, records to Key Records
   - `Taim.Data/CLAUDE.md` — add table to Tables table
   - `Taim.Api/CLAUDE.md` — add routes to Endpoint Groups table
   - Root `CLAUDE.md` — update build state if this is a sprint feature

10. **Rebuild and redeploy**:
    ```bash
    cd src/backend && dotnet build Taim.slnx
    docker compose build taim-api && docker compose up -d taim-api
    ```
