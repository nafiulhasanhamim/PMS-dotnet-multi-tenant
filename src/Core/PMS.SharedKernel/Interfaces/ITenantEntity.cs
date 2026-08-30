namespace PMS.SharedKernel.Interfaces;

/// <summary>
/// Marks an entity as belonging to exactly one tenant (one pharmacy).
///
/// Every entity carrying this interface is automatically:
/// - filtered to the current tenant by a global query filter, and
/// - stamped with the current tenant on insert by TenantEntityInterceptor.
///
/// Both happen by convention in the persistence layer, so a new entity becomes
/// tenant-isolated simply by implementing this. Nothing needs to remember to add a
/// WHERE clause — which is the whole point, because the one that gets forgotten is
/// the one that leaks another pharmacy's data.
/// </summary>
public interface ITenantEntity
{
    /// <summary>
    /// The tenant that owns this row. Set once on insert and never changed.
    /// </summary>
    Guid TenantId { get; }
}
