using PMS.Domain.Enums;

namespace PMS.Application.Interfaces;

/// <summary>An issued token and when it stops being valid.</summary>
public sealed record TokenResult(string Token, DateTime ExpiresAtUtc);

/// <summary>Issues the two kinds of token this system uses.</summary>
public interface IJwtTokenService
{
    /// <summary>
    /// A token for a pharmacy session: carries the tenant and the role held *there*.
    /// The same person signing in at a different pharmacy gets a different role.
    /// </summary>
    TokenResult IssueForTenantUser(Guid userId, Guid tenantId, UserRole role);

    /// <summary>
    /// A token for a platform operator: carries no tenant and no role, so every tenant query
    /// filter it meets resolves to nothing.
    /// </summary>
    TokenResult IssueForPlatformAdmin(Guid userId);
}
