namespace PMS.SharedKernel.Interfaces;

/// <summary>
/// The tenant the current request belongs to.
///
/// Resolved from a signed claim on the authenticated principal, never from anything the
/// caller supplies directly (a header or a route value can be edited by hand; a claim
/// inside a signed token cannot).
/// </summary>
public interface ICurrentTenantService
{
    /// <summary>
    /// The current tenant, or <see cref="Guid.Empty"/> when no tenant could be resolved.
    ///
    /// Empty is deliberately not a wildcard. The global query filter compares against this
    /// value directly, so an unresolved tenant matches no rows at all. An anonymous or
    /// misconfigured request therefore sees nothing rather than everything — the failure
    /// mode has to be an empty screen, never another pharmacy's stock.
    /// </summary>
    Guid TenantId { get; }

    /// <summary>
    /// True when a tenant was resolved.
    /// </summary>
    bool HasTenant { get; }

    /// <summary>
    /// True for a request carrying the platform_admin claim — a platform operator, working
    /// outside any pharmacy.
    ///
    /// This does not widen the query filter. Crossing the tenant boundary is always an
    /// explicit act at the call site — see <c>IgnoreQueryFilters()</c> — so that reading
    /// another pharmacy's data is something you can find by searching for it, rather than
    /// something that happens quietly because of who is signed in.
    /// </summary>
    bool IsPlatformAdmin { get; }
}
