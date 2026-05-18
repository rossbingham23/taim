using Microsoft.EntityFrameworkCore;
using Taim.Core.Reports;
using Taim.Data.Models;

namespace Taim.Data.Services;

public sealed class ReportService(TaimDbContext db) : IReportService
{
    public async Task<ExecutiveReportRecord> SaveAsync(SaveReportRequest request, CancellationToken ct = default)
    {
        var entity = new ExecutiveReportEntity
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            TaskId = request.TaskId,
            AgentId = request.AgentId,
            Title = request.Title,
            Content = request.Content,
            ReportType = "kickoff",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.ExecutiveReports.Add(entity);
        await db.SaveChangesAsync(ct);

        return new ExecutiveReportRecord(
            entity.Id, entity.TenantId, entity.TaskId,
            entity.AgentId, request.AgentName,
            entity.Title, entity.Content, entity.CreatedAt);
    }

    public async Task<IReadOnlyList<ExecutiveReportRecord>> GetTaskReportsAsync(
        Guid tenantId, Guid taskId, CancellationToken ct = default)
    {
        var rows = await db.ExecutiveReports
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.TaskId == taskId)
            .Join(db.Agents.AsNoTracking(), r => r.AgentId, a => a.Id,
                (r, a) => new
                {
                    r.Id, r.TenantId, r.TaskId, r.AgentId,
                    AgentName = a.Name,
                    r.Title, r.Content, r.CreatedAt
                })
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(r => new ExecutiveReportRecord(
            r.Id, r.TenantId, r.TaskId, r.AgentId, r.AgentName,
            r.Title, r.Content, r.CreatedAt)).ToList();
    }
}
