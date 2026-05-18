# Taim.Data — Data Layer

## Key Files

| File | Purpose |
|---|---|
| `TaimDbContext.cs` | EF Core DbContext with all entity configurations |
| `Models/Entities.cs` | All EF entity classes |
| `RlsInterceptor.cs` | Sets `app.tenant_id` on every connection (PostgreSQL RLS) |
| `DataExtensions.cs` | `AddTaimData()` — registers all services |

## EF Core Column Mapping

**All column names are mapped explicitly** via `.HasColumnName()`. There is no snake_case convention. When adding a new property, you MUST add the mapping:

```csharp
e.Property(x => x.MyField).HasColumnName("my_field");
```

Forgetting this causes EF to use the C# PascalCase name as the column, breaking queries silently.

## Row-Level Security (RLS)

`RlsInterceptor` runs before every command and executes:

```sql
SET LOCAL app.tenant_id = '<tenantId>';
```

PostgreSQL RLS policies on all tables enforce tenant isolation automatically. The interceptor gets `TenantId` from `TenantIdAccessor` (Scoped). **Do not use `AddDbContextPool`** — the pool doesn't support Scoped interceptors.

## Tables

| Table | Entity | Service |
|---|---|---|
| `tenants` | `TenantEntity` | (direct query in AuthEndpoints) |
| `users` | `UserEntity` | (direct query in AuthEndpoints) |
| `tenant_provider_configs` | `TenantProviderConfigEntity` | `TenantProviderResolver` |
| `tasks` | `TaskEntity` | `TaskService` |
| `agents` | `AgentEntity` | `AgentRegistryService` |
| `budgets` | `BudgetEntity` | `BudgetService` |
| `spend_entries` | `SpendEntryEntity` | `BudgetService` |
| `actions` | `ActionEntity` | `ActionService` |
| `kpis` | `KpiEntity` | `KpiService` |
| `kpi_values` | `KpiValueEntity` | `KpiService` |
| `approvals` | `ApprovalEntity` | `ApprovalService` |
| `meetings` | `MeetingEntity` | `MeetingService` |
| `meeting_messages` | `MeetingMessageEntity` | `MeetingService` |
| `meeting_participants` | `MeetingParticipantEntity` | `MeetingService` |
| `executive_reports` | `ExecutiveReportEntity` | `ReportService` |
| `agent_chat_history` | `AgentChatHistoryEntity` | `ChatHistoryProvider` (Taim.Memory) |
| `memory_entries` | `MemoryEntryEntity` | semantic memory |
| `scheduled_tasks` | `ScheduledTaskEntity` | workflow scheduling |

## Schema Migration

`infra/init.sql` is the source of truth — it creates all tables and RLS policies. There are **no EF migrations**. To change the schema:
1. Update `init.sql`
2. Update the corresponding entity class in `Models/Entities.cs`
3. Update the EF configuration in `TaimDbContext.OnModelCreating`
4. Rebuild the Docker PostgreSQL volume: `docker compose down -v && ./start.sh`

## Enum Storage

Enums are stored as lowercase strings by calling `.ToString().ToLowerInvariant()` on the C# enum value. Example: `AgentStatus.WaitingApproval` → `"waitingapproval"` (no underscore). When reading back, parse via a switch or `Enum.Parse`.
