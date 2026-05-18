namespace Taim.Core.Reports;

public sealed record ExecutiveReportRecord(
    Guid Id,
    Guid TenantId,
    Guid? TaskId,
    Guid AgentId,
    string AgentName,
    string Title,
    string Content,
    DateTimeOffset GeneratedAt
);

public sealed record SaveReportRequest(
    Guid TenantId,
    Guid? TaskId,
    Guid AgentId,
    string AgentName,
    string Title,
    string Content
);

public interface IReportService
{
    Task<ExecutiveReportRecord> SaveAsync(SaveReportRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ExecutiveReportRecord>> GetTaskReportsAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);
}
