using System.Security.Claims;
using PMS.SharedKernel.Interfaces;
using Microsoft.AspNetCore.Http;

namespace PMS.Infrastructure.Services;

/// <summary>
/// Reads the current tenant from the authenticated principal's claims.
/// </summary>
public class TenantContext : ITenantContext
{
    /// <summary>Claim carrying the tenant id. Issued at login, signed with the token.</summary>
    public const string TenantIdClaim = "tenant_id";

    /// <summary>Role that works across tenants.</summary>
    public const string PlatformAdminRole = "PlatformAdmin";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public Guid TenantId
    {
        get
        {
            var raw = _httpContextAccessor.HttpContext?.User?.FindFirstValue(TenantIdClaim);
            // Anything unparseable is treated as "no tenant", which matches no rows.
            // Falling back to empty rather than throwing keeps unauthenticated endpoints
            // (login, health) working while still showing them no tenant data.
            return Guid.TryParse(raw, out var tenantId) ? tenantId : Guid.Empty;
        }
    }

    /// <inheritdoc />
    public bool HasTenant => TenantId != Guid.Empty;

    /// <inheritdoc />
    public bool IsPlatformAdmin =>
        _httpContextAccessor.HttpContext?.User?.IsInRole(PlatformAdminRole) ?? false;
}
