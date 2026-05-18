using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using System.Data.Common;

namespace Taim.Data;

/// <summary>
/// Sets the app.tenant_id PostgreSQL session variable on every connection open,
/// enabling row-level security (RLS) to automatically filter all queries to the
/// current tenant without any application-level WHERE clauses.
/// </summary>
public sealed class RlsInterceptor : DbConnectionInterceptor
{
    private readonly ITenantIdAccessor _tenantIdAccessor;

    public RlsInterceptor(ITenantIdAccessor tenantIdAccessor)
    {
        _tenantIdAccessor = tenantIdAccessor;
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (_tenantIdAccessor.TenantId is { } tenantId && connection is NpgsqlConnection npgsql)
        {
            using var cmd = npgsql.CreateCommand();
            cmd.CommandText = $"SET app.tenant_id = '{tenantId}'";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}

/// <summary>
/// Provides the current tenant ID scoped to the request/operation.
/// Populated by API middleware from the JWT claim.
/// </summary>
public interface ITenantIdAccessor
{
    Guid? TenantId { get; }
}

/// <summary>
/// Mutable accessor — the API middleware sets TenantId after authenticating.
/// Registered as Scoped so each HTTP request gets its own instance.
/// </summary>
public sealed class TenantIdAccessor : ITenantIdAccessor
{
    public Guid? TenantId { get; set; }
}
