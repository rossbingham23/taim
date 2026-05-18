namespace Taim.Core.Tenancy;

public interface ITenantContext
{
    Guid TenantId { get; }
}

public sealed record TenantContext(Guid TenantId) : ITenantContext;
