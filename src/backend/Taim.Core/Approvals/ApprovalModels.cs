namespace Taim.Core.Approvals;

public enum ApprovalStatus { Pending, Approved, Denied }

public enum ApprovalScope
{
    Once,                   // approve this one call
    AgentAndTool,           // agent X can always call tool Y
    AgentToolAndParam       // agent X can always call tool Y with param Z
}

public sealed record ApprovalRequest(
    Guid Id,
    Guid TenantId,
    Guid AgentId,
    string ToolName,
    Dictionary<string, object?> ToolArguments,
    string Description,
    ApprovalStatus Status,
    ApprovalScope Scope,
    string? ScopeKey,
    DateTimeOffset? DecidedAt,
    string? DurableRequestId,
    DateTimeOffset CreatedAt
);

public sealed record ApprovalDecision(
    Guid ApprovalId,
    bool Approved,
    ApprovalScope Scope,
    string? ScopeKey = null
);

public interface IApprovalService
{
    Task<ApprovalRequest> CreateAsync(Guid tenantId, Guid agentId, string toolName,
        Dictionary<string, object?> toolArguments, string description,
        string? durableRequestId = null, CancellationToken ct = default);

    Task<ApprovalDecision?> CheckLongLivedAsync(Guid tenantId, Guid agentId,
        string toolName, Dictionary<string, object?> toolArguments, CancellationToken ct = default);

    Task ApplyDecisionAsync(Guid tenantId, ApprovalDecision decision, CancellationToken ct = default);

    Task<IReadOnlyList<ApprovalRequest>> GetPendingAsync(Guid tenantId, CancellationToken ct = default);
}
