# Taim.Core — Domain Model

Pure domain layer. No external dependencies — only .NET BCL. All other projects depend on this.

## Interfaces

| Interface | File | Description |
|---|---|---|
| `IActionService` | `Actions/ActionModels.cs` | Work-item (action) CRUD for tasks and agents |
| `IAgentRegistry` | `Agents/AgentModels.cs` | CRUD for agent definitions; status updates |
| `IKpiService` | `KPIs/KpiModels.cs` | Create KPIs, record values, get hierarchy |
| `IReportService` | `Reports/ReportModels.cs` | Save and retrieve executive reports |
| `ITaskService` | `Teams/TeamModels.cs` | Task creation, status, team graph |
| `IBudgetService` | `Budget/BudgetModels.cs` | Budget tracking and enforcement |
| `IApprovalService` | `Approvals/ApprovalModels.cs` | Approval request lifecycle |
| `IMeetingStore` | `Meetings/MeetingModels.cs` | Meeting creation, message storage, task-scoped lookup |
| `IMeetingOrchestrator` | `Meetings/IMeetingOrchestrator.cs` | Runs turn-based LLM meetings to completion |
| `INotificationService` | `Notifications/INotificationChannel.cs` | Broadcast to all `INotificationChannel` implementations |
| `INotificationChannel` | `Notifications/INotificationChannel.cs` | Channel adapter (e.g. SignalR hub) |
| `IMemoryService` | `Memory/IMemoryService.cs` | Semantic memory (embedding + retrieval) |
| `IProviderFactory` | `Providers/IProviderConfig.cs` | Create `IChatClient` for a given provider/tenant |
| `ITenantIdAccessor` | `Tenancy/ITenantContext.cs` | Provides `TenantId` for the current scope |

## Key Enums

All enums are serialized as **camelCase strings** over HTTP (`JsonStringEnumConverter(CamelCase)`).
SignalR notifications use **snake_case strings** (`JsonStringEnumConverter(SnakeCaseLower)`).
DB storage uses `.ToString().ToLowerInvariant()` (no underscore — pre-existing convention).

```csharp
AgentRole   { Bootstrap, Expert, Ceo, Cto, Cmo, Cfo, Hr, ProductManager, Developer,
              Designer, QaEngineer, QaManager, MarketingSpecialist, ContentWriter,
              DataAnalyst, SalesRepresentative, CustomerSupport, Generic }

AgentStatus { Idle, Active, WaitingApproval, Sleeping, Terminated }

NotificationKind { ApprovalRequired, AgentStatusChanged, ExecutiveReport,
                   BudgetAlert, TeamUpdate,
                   MeetingStarted, MeetingMessage, MeetingCompleted,
                   AgentLog, ActionCreated, ActionUpdated }

KpiDirection { HigherIsBetter, LowerIsBetter, TargetValue }
```

## Key Records

```csharp
AgentDefinition(Id, TenantId, TaskId?, ParentAgentId?, Name, Role, Charter, Status,
                Provider?, Model?, DurableEntityKey?, CreatedAt, UpdatedAt)

CreateAgentRequest(TenantId, TaskId?, ParentAgentId?, Name, Role, Charter,
                   Provider?, Model?, BudgetId?)

ExecutiveReportRecord(Id, TenantId, TaskId?, AgentId, AgentName, Title, Content, GeneratedAt)

SaveReportRequest(TenantId, TaskId?, AgentId, AgentName, Title, Content)

CreateKpiRequest(TenantId, AgentId, ParentKpiId?, Name, Description?, TargetValue?,
                 Unit?, Direction)

Notification(Id, TenantId, Kind, Title, Body, Metadata, CreatedAt)

ActionRecord(Id, TenantId, TaskId, AgentId?, CreatedByAgentId?, Title, Description?,
             Status, Priority, ParentActionId?, DueAt?, CompletedAt?, CreatedAt, UpdatedAt)

CreateActionRequest(TenantId, TaskId, AgentId?, CreatedByAgentId?, Title, Description?,
                    Priority, ParentActionId?, DueAt?)

UpdateActionRequest(Status?, Title?, Description?, Priority?, AgentId?, DueAt?)
```
