using PMS.Application.Common.DTOs;

namespace PMS.Application.Interfaces;

/// <summary>Platform-level reads, outside any pharmacy.</summary>
public interface IPlatformQueries
{
    Task<IReadOnlyList<TenantDto>> ListTenantsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads and writes over the current pharmacy's staff.
///
/// Everything here goes through the membership query filter — no method takes a tenant id,
/// because supplying one would be an opportunity to supply the wrong one.
/// </summary>
public interface ITenantUserQueries
{
    Task<IReadOnlyList<TenantUserDto>> ListForCurrentTenantAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Returns null when the membership is not in the current pharmacy.</summary>
    Task<TenantUserDto?> SetActiveAsync(
        Guid membershipId, bool isActive, CancellationToken cancellationToken = default);
}
