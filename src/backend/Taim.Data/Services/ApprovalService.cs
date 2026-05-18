using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Taim.Core.Approvals;
using Taim.Core.Notifications;
using Taim.Data.Models;

namespace Taim.Data.Services;

public sealed class ApprovalService(TaimDbContext db, INotificationService notifications) : IApprovalService
{
    public async Task<ApprovalRequest> CreateAsync(
        Guid tenantId,
        Guid agentId,
        string toolName,
        Dictionary<string, object?> toolArguments,
        string description,
        string? durableRequestId = null,
        CancellationToken ct = default)
    {
        var entity = new ApprovalEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AgentId = agentId,
            ToolName = toolName,
            ToolArguments = JsonDocument.Parse(JsonSerializer.Serialize(toolArguments)),
            Description = description,
            Status = "pending",
            Scope = "once",
            DurableRequestId = durableRequestId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Approvals.Add(entity);
        await db.SaveChangesAsync(ct);

        await notifications.NotifyAsync(
            tenantId,
            NotificationKind.ApprovalRequired,
            $"Approval Required: {toolName}",
            description,
            new Dictionary<string, object?> { ["approvalId"] = entity.Id.ToString(), ["agentId"] = agentId.ToString(), ["toolName"] = toolName },
            ct);

        return ToRecord(entity);
    }

    public async Task<ApprovalDecision?> CheckLongLivedAsync(
        Guid tenantId,
        Guid agentId,
        string toolName,
        Dictionary<string, object?> toolArguments,
        CancellationToken ct = default)
    {
        // Check for an agent+tool long-lived approval
        var agentToolKey = $"{agentId}:{toolName}";
        var existing = await db.Approvals
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId
                     && a.AgentId == agentId
                     && a.ToolName == toolName
                     && a.Status == "approved"
                     && (a.Scope == "agent_and_tool" || a.Scope == "agent_tool_and_param"))
            .FirstOrDefaultAsync(ct);

        if (existing is null) return null;

        if (existing.Scope == "agent_tool_and_param" && existing.ScopeKey is not null)
        {
            // Verify the scope key matches the argument fingerprint
            var fingerprint = ComputeFingerprint(toolArguments);
            if (existing.ScopeKey != fingerprint) return null;
        }

        return new ApprovalDecision(
            existing.Id,
            Approved: true,
            Enum.TryParse<ApprovalScope>(existing.Scope, true, out var scope) ? scope : ApprovalScope.Once,
            existing.ScopeKey);
    }

    public async Task ApplyDecisionAsync(Guid tenantId, ApprovalDecision decision, CancellationToken ct = default)
    {
        var entity = await db.Approvals
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == decision.ApprovalId, ct);

        if (entity is null) return;

        entity.Status = decision.Approved ? "approved" : "denied";
        entity.Scope = decision.Scope.ToString().ToSnakeCase();
        entity.ScopeKey = decision.ScopeKey;
        entity.DecidedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ApprovalRequest>> GetPendingAsync(Guid tenantId, CancellationToken ct = default)
    {
        var entities = await db.Approvals
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.Status == "pending")
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(ToRecord).ToList();
    }

    private static string ComputeFingerprint(Dictionary<string, object?> args) =>
        System.Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(args))));

    private static ApprovalRequest ToRecord(ApprovalEntity e) => new(
        e.Id,
        e.TenantId,
        e.AgentId,
        e.ToolName,
        e.ToolArguments is not null
            ? JsonSerializer.Deserialize<Dictionary<string, object?>>(e.ToolArguments.RootElement.GetRawText()) ?? new()
            : new(),
        e.Description,
        Enum.TryParse<ApprovalStatus>(e.Status, true, out var status) ? status : ApprovalStatus.Pending,
        Enum.TryParse<ApprovalScope>(e.Scope?.Replace("_", ""), true, out var scope) ? scope : ApprovalScope.Once,
        e.ScopeKey,
        e.DecidedAt,
        e.DurableRequestId,
        e.CreatedAt);
}

internal static class StringExtensions
{
    internal static string ToSnakeCase(this string s) => s switch
    {
        "Once" => "once",
        "AgentAndTool" => "agent_and_tool",
        "AgentToolAndParam" => "agent_tool_and_param",
        _ => s.ToLowerInvariant()
    };
}
