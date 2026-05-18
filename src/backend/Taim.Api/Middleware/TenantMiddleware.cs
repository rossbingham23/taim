using System.Security.Claims;
using Taim.Data;

namespace Taim.Api.Middleware;

/// <summary>
/// Reads the tenantId claim from the authenticated JWT and sets it on TenantIdAccessor.
/// This activates the PostgreSQL RLS interceptor for the duration of the request.
/// Must be registered after UseAuthentication.
/// </summary>
public sealed class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, TenantIdAccessor tenantIdAccessor)
    {
        if (ctx.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = ctx.User.FindFirst("tenantId")?.Value
                           ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (tenantClaim is not null && Guid.TryParse(tenantClaim, out var tenantId))
                tenantIdAccessor.TenantId = tenantId;
        }

        await next(ctx);
    }
}
