namespace Taim.Core.Agents;

public enum AgentRole
{
    Bootstrap,
    Expert,
    Ceo,
    Cto,
    Cmo,
    Cfo,
    Hr,
    ProductManager,
    Developer,
    Designer,
    QaEngineer,
    QaManager,
    MarketingSpecialist,
    ContentWriter,
    DataAnalyst,
    SalesRepresentative,
    CustomerSupport,
    Generic
}

public enum AgentStatus
{
    Idle,
    Active,
    WaitingApproval,
    Sleeping,       // in a durable timer
    Terminated
}

public sealed record AgentDefinition(
    Guid Id,
    Guid TenantId,
    Guid? TaskId,
    Guid? ParentAgentId,
    string Name,
    AgentRole Role,
    string Charter,
    AgentStatus Status,
    string? Provider,
    string? Model,
    string? DurableEntityKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record CreateAgentRequest(
    Guid TenantId,
    Guid? TaskId,
    Guid? ParentAgentId,
    string Name,
    AgentRole Role,
    string Charter,
    string? Provider = null,
    string? Model = null,
    Guid? BudgetId = null
);

public interface IAgentRegistry
{
    Task<AgentDefinition> RegisterAsync(CreateAgentRequest request, CancellationToken ct = default);
    Task<AgentDefinition?> GetAsync(Guid tenantId, Guid agentId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentDefinition>> GetTeamAsync(Guid tenantId, Guid? parentAgentId, CancellationToken ct = default);
    Task UpdateStatusAsync(Guid tenantId, Guid agentId, AgentStatus status, CancellationToken ct = default);
    Task SetDurableEntityKeyAsync(Guid tenantId, Guid agentId, string entityKey, CancellationToken ct = default);
}
