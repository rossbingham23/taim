namespace Taim.Core.Scheduling;

public enum ScheduledTaskStatus { Active, Paused, Deleted }

public sealed record ScheduledTask(
    Guid Id,
    Guid TenantId,
    Guid AgentId,
    string Name,
    string CronExpression,
    string Prompt,
    ScheduledTaskStatus Status,
    string? DurableInstanceId,
    DateTimeOffset? LastRunAt,
    DateTimeOffset? NextRunAt,
    DateTimeOffset CreatedAt
);

public sealed record CreateScheduledTaskRequest(
    Guid TenantId,
    Guid AgentId,
    string Name,
    string CronExpression,
    string Prompt
);

public interface ISchedulingService
{
    Task<ScheduledTask> CreateAsync(CreateScheduledTaskRequest request, CancellationToken ct = default);
    Task PauseAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);
    Task ResumeAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);
    Task DeleteAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);
    Task<IReadOnlyList<ScheduledTask>> GetForAgentAsync(Guid tenantId, Guid agentId, CancellationToken ct = default);
}
