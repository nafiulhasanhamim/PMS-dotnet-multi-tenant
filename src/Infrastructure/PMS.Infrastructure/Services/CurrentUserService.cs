using System.Security.Claims;
using PMS.Application.Common.Security;
using PMS.SharedKernel.Interfaces;
using Microsoft.AspNetCore.Http;

namespace PMS.Infrastructure.Services;

/// <summary>
/// Provides access to the current authenticated user's information from HttpContext.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public string? UserId =>
        // Our tokens put the user id in `sub`; fall back to the framework claim so anything
        // issued by another scheme still resolves.
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(AuthClaims.Subject)
        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <inheritdoc />
    public Guid? UserGuid =>
        Guid.TryParse(UserId, out var id) ? id : null;

    /// <inheritdoc />
    public string? TenantRoleName =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(AuthClaims.Role);

    /// <inheritdoc />
    public string? UserName => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);

    /// <inheritdoc />
    public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);

    /// <inheritdoc />
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public IEnumerable<string> Roles => _httpContextAccessor.HttpContext?.User?.Claims
        .Where(c => c.Type == ClaimTypes.Role)
        .Select(c => c.Value) ?? [];

    /// <inheritdoc />
    public bool IsInRole(string role)
    {
        return _httpContextAccessor.HttpContext?.User?.IsInRole(role) ?? false;
    }
}
