using PMS.SharedKernel.Interfaces;

namespace PMS.SharedKernel.Common;

/// <summary>
/// Base class for entities that support soft deletion.
/// Soft-deleted entities are not physically removed but marked as deleted.
/// Use EF Core global query filters to automatically exclude soft-deleted entities.
/// </summary>
/// <typeparam name="TId">The type of the entity identifier.</typeparam>
public abstract class SoftDeletableEntity<TId> : AuditableEntity<TId>, ISoftDelete
    where TId : notnull
{
    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTime? DeletedOnUtc { get; set; }

    /// <inheritdoc />
    public string? DeletedBy { get; set; }
}

/// <summary>
/// Soft deletable entity with integer identifier (convenience class).
/// </summary>
public abstract class SoftDeletableEntity : SoftDeletableEntity<int>
{
}
