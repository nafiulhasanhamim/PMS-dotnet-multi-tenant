using PMS.SharedKernel.Interfaces;

namespace PMS.SharedKernel.Common;

/// <summary>
/// Base class for entities that track creation and modification metadata.
/// </summary>
/// <typeparam name="TId">The type of the entity identifier.</typeparam>
public abstract class AuditableEntity<TId> : BaseEntity<TId>, IAuditable
    where TId : notnull
{
    /// <inheritdoc />
    public DateTime CreatedOnUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedOnUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }
}

/// <summary>
/// Auditable entity with integer identifier (convenience class).
/// </summary>
public abstract class AuditableEntity : AuditableEntity<int>
{
}
