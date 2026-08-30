namespace PMS.SharedKernel.Interfaces;

/// <summary>
/// Marker interface for entities that support soft deletion.
/// EF Core global query filters should automatically exclude soft-deleted entities.
/// </summary>
public interface ISoftDelete
{
    /// <summary>
    /// Gets or sets a value indicating whether this entity has been soft-deleted.
    /// </summary>
    bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this entity was deleted.
    /// </summary>
    DateTime? DeletedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who deleted this entity.
    /// </summary>
    string? DeletedBy { get; set; }
}
