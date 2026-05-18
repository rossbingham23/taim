using System.Text.Json;
using Pgvector;

namespace Taim.Data.Models;

public class TenantEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}

public class UserEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}

public class TenantProviderConfigEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Provider { get; set; } = null!;
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public string DefaultModel { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class BudgetEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? TaskId { get; set; }
    public decimal LimitUsd { get; set; }
    public decimal SpentUsd { get; set; }
    public string Status { get; set; } = "active";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<SpendEntryEntity> SpendEntries { get; set; } = [];
}

public class SpendEntryEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BudgetId { get; set; }
    public Guid AgentId { get; set; }
    public string Provider { get; set; } = null!;
    public string Model { get; set; } = null!;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal CostUsd { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

public class TaskEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Goal { get; set; } = null!;
    public string Status { get; set; } = "pending";
    public Guid? BudgetId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<AgentEntity> Agents { get; set; } = [];
}

public class AgentEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? ParentAgentId { get; set; }
    public string Name { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string? Charter { get; set; }
    public string Status { get; set; } = "idle";
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? DurableEntityKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<KpiEntity> Kpis { get; set; } = [];
}

public class KpiEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AgentId { get; set; }
    public Guid? ParentKpiId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? TargetValue { get; set; }
    public string? Unit { get; set; }
    public string Direction { get; set; } = "higher_is_better";
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<KpiValueEntity> Values { get; set; } = [];
}

public class KpiValueEntity
{
    public Guid Id { get; set; }
    public Guid KpiId { get; set; }
    public Guid TenantId { get; set; }
    public string Value { get; set; } = null!;
    public DateTimeOffset RecordedAt { get; set; }
    public string? Source { get; set; }
}

public class ApprovalEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AgentId { get; set; }
    public string ToolName { get; set; } = null!;
    public JsonDocument? ToolArguments { get; set; }
    public string Description { get; set; } = null!;
    public string Status { get; set; } = "pending";
    public string Scope { get; set; } = "once";
    public string? ScopeKey { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DurableRequestId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class MeetingEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? TaskId { get; set; }
    public string Topic { get; set; } = null!;
    public string MeetingType { get; set; } = "kickoff_sync";
    public string Status { get; set; } = "in_progress";
    public Guid? OrganizerAgentId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string? Summary { get; set; }

    public ICollection<MeetingMessageEntity> Messages { get; set; } = [];
    public ICollection<MeetingParticipantEntity> Participants { get; set; } = [];
}

public class MeetingParticipantEntity
{
    public Guid MeetingId { get; set; }
    public Guid AgentId { get; set; }
    public string Role { get; set; } = "participant";
}

public class MeetingMessageEntity
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? AgentId { get; set; }
    public string Role { get; set; } = null!;
    public string Content { get; set; } = null!;
    public int Sequence { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class ScheduledTaskEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AgentId { get; set; }
    public string Name { get; set; } = null!;
    public string CronExpression { get; set; } = null!;
    public string Prompt { get; set; } = null!;
    public string Status { get; set; } = "active";
    public string? DurableInstanceId { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class MemoryEntryEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? AgentId { get; set; }
    public string Collection { get; set; } = "default";
    public string Content { get; set; } = null!;
    public Vector? Embedding { get; set; }
    public JsonDocument? Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class ExecutiveReportEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid AgentId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string ReportType { get; set; } = "status";
    public DateTimeOffset CreatedAt { get; set; }
}

public class ActionEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid TaskId { get; set; }
    public Guid? AgentId { get; set; }
    public Guid? CreatedByAgentId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string Status { get; set; } = "open";
    public int Priority { get; set; } = 50;
    public Guid? ParentActionId { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class AgentChatHistoryEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AgentId { get; set; }
    public string SessionId { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string Content { get; set; } = null!;
    public JsonDocument? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }
    public int Sequence { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
