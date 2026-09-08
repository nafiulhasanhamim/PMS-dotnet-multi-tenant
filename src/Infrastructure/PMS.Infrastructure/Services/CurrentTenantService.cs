using System.Security.Claims;
using PMS.Application.Common.Security;
using PMS.SharedKernel.Interfaces;
using Microsoft.AspNetCore.Http;

namespace PMS.Infrastructure.Services;

/// <summary>
/// Reads the current tenant from the authenticated principal's claims.
/// </summary>
public class CurrentTenantService : ICurrentTenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public Guid TenantId
    {
        get
        {
            var raw = _httpContextAccessor.HttpContext?.User?.FindFirstValue(AuthClaims.TenantId);
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
        string.Equals(
            _httpContextAccessor.HttpContext?.User?.FindFirstValue(AuthClaims.PlatformAdmin),
            "true", StringComparison.OrdinalIgnoreCase);
}
