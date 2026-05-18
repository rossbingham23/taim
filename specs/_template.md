---
id: SN-NNN
title: Feature Title
sprint: N
status: draft
created: YYYY-MM-DD
updated: YYYY-MM-DD
---

# SN-NNN — Feature Title

## Problem Statement

> Why does this feature exist? What user or agent need does it address? What breaks or is impossible without it?

One paragraph. Focus on the problem, not the solution.

## Solution Overview

> High-level description of the approach. What will be built? How does it fit into the existing architecture?

One to three paragraphs.

## Data Model

> Changes to the PostgreSQL schema and EF entities. Skip this section if no DB changes.

### SQL (add to `infra/postgres/init.sql`)

```sql
CREATE TABLE xxx (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id   UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    -- fields...
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_xxx_tenant ON xxx(tenant_id);
ALTER TABLE xxx ENABLE ROW LEVEL SECURITY;
-- Add 'xxx' to the RLS tables array in the DO $$ block
```

### EF Entity (add to `Taim.Data/Models/Entities.cs`)

```csharp
public class XxxEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    // fields...
    public DateTimeOffset CreatedAt { get; set; }
}
```

### EF Configuration (add to `TaimDbContext.OnModelCreating`)

```csharp
modelBuilder.Entity<XxxEntity>(e =>
{
    e.ToTable("xxx");
    e.HasKey(x => x.Id);
    e.Property(x => x.Id).HasColumnName("id");
    e.Property(x => x.TenantId).HasColumnName("tenant_id");
    // ...
});
```

## Core Interface (add to `Taim.Core/Xxx/XxxModels.cs`)

```csharp
public sealed record XxxRecord(Guid Id, Guid TenantId, ...);
public sealed record CreateXxxRequest(...);

public interface IXxxService
{
    Task<XxxRecord> CreateAsync(CreateXxxRequest request, CancellationToken ct = default);
    // ...
}
```

## API Contract

> New or changed HTTP endpoints. Skip if no API changes.

### `GET /api/xxx?taskId=`
- Auth: Bearer required
- Query: `taskId` (required)
- Response 200: `XxxRecord[]`
- Response 400: `{ "error": "taskId query parameter is required" }`

### `POST /api/xxx`
- Auth: Bearer required
- Request: `{ "taskId": "uuid", "title": "string" }`
- Response 201: `XxxRecord`

### `PATCH /api/xxx/{id}`
- Auth: Bearer required
- Request: `{ "status": "string" }` (all fields optional)
- Response 200: `XxxRecord`
- Response 404: not found

## UI/UX

> ASCII wireframe of the new or changed UI. Skip if no frontend changes.

```
┌─────────────────────────────────────────────────────────┐
│ TeamView                                                 │
│                                                         │
│  [Team Structure]    [Team (4)]                         │
│                                                         │
│  [Activity ▼]                                           │
│  ─────────────────────────────────────                  │
│  12:34 LOG   Agent activated                            │
│  12:35 REPORT  CEO Kickoff Strategy                     │
│                                                         │
│  ┌──────────── sidebar ────────────┐                    │
│  │ ACTIONS (3)                     │                    │
│  │ ▐ Define tech stack  [CTO]      │                    │
│  │ ▐ Draft go-to-market [CMO]      │                    │
│  │ ▐ Hire engineering   [HR ]      │                    │
│  └─────────────────────────────────┘                    │
└─────────────────────────────────────────────────────────┘
```

## Acceptance Criteria

- [ ] **AC-1**: [Specific, testable, binary criterion]
- [ ] **AC-2**: [Specific, testable, binary criterion]
- [ ] **AC-3**: ...

## Test Plan

**Smoke test** (add to `Taim.Tests/XxxTests.cs`):
- [ ] `GET /api/xxx?taskId=` returns 200 with empty array for new task
- [ ] `POST /api/xxx` creates a record and returns 201
- [ ] `PATCH /api/xxx/{id}` updates status

**E2E addition** (update `src/ui-tests/tests/user-journey.spec.ts`):
- [ ] After team assembles, Actions panel appears in sidebar with at least one item (if delegation dispatch fires)

## CLAUDE.md Updates Required

- [ ] `Taim.Core/CLAUDE.md` — add `IXxxService` to interfaces table
- [ ] `Taim.Data/CLAUDE.md` — add `xxx` table entry
- [ ] `Taim.Api/CLAUDE.md` — add endpoints to Endpoint Groups table
- [ ] Root `CLAUDE.md` — update build state table

## Review

> Appended by reviewer after implementation.

**Date:** —
**Result:** —
**Notes:** —
