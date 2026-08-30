namespace PMS.SharedKernel.Grid;

/// <summary>
/// Describes a sort operation to apply to grid data.
/// </summary>
public class SortDescriptor
{
    /// <summary>
    /// Gets or sets the field/property name to sort by.
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort direction (asc or desc).
    /// </summary>
    public string Dir { get; set; } = "asc";

    /// <summary>
    /// Gets a value indicating whether this is a descending sort.
    /// </summary>
    public bool IsDescending => Dir?.Equals("desc", StringComparison.OrdinalIgnoreCase) == true;
}
