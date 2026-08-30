namespace PMS.SharedKernel.Interfaces;

/// <summary>
/// Marker interface for entities that track audit information.
/// EF Core interceptors/SaveChanges will automatically populate these fields.
/// </summary>
public interface IAuditable
{
    /// <summary>
    /// Gets or sets the UTC timestamp when this entity was created.
    /// </summary>
    DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who created this entity.
    /// </summary>
    string? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this entity was last modified.
    /// </summary>
    DateTime? ModifiedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who last modified this entity.
    /// </summary>
    string? ModifiedBy { get; set; }
}
