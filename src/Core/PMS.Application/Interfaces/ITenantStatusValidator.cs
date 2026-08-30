namespace PMS.Application.Interfaces;

/// <summary>
/// Whether a tenant is currently allowed to be used.
/// </summary>
public enum TenantStatus
{
    /// <summary>The tenant exists, is not deleted, and is active.</summary>
    Ok,

    /// <summary>No such tenant, or it has been soft-deleted.</summary>
    NotFound,

    /// <summary>The tenant exists but has been suspended.</summary>
    Inactive,
}

/// <summary>
/// Checks a tenant's status.
///
/// Deliberately separate from ITenantContext. That one reads a claim and costs nothing, and
/// is consulted constantly — by every query filter. This one hits the database, so it is
/// called once per request at the edge rather than on every read.
/// </summary>
public interface ITenantStatusValidator
{
    Task<TenantStatus> CheckAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
