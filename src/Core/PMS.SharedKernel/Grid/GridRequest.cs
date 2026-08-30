namespace PMS.SharedKernel.Grid;

/// <summary>
/// Represents a request for paginated, filtered, and sorted grid data.
/// Compatible with Telerik/Kendo UI grid state format.
/// </summary>
public class GridRequest
{
    /// <summary>
    /// Gets or sets the number of items to skip (for pagination).
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Gets or sets the number of items to take (page size).
    /// </summary>
    public int Take { get; set; } = 20;

    /// <summary>
    /// Gets or sets the sort descriptors.
    /// </summary>
    public List<SortDescriptor>? Sort { get; set; }

    /// <summary>
    /// Gets or sets the filter descriptor.
    /// </summary>
    public FilterDescriptor? Filter { get; set; }

    /// <summary>
    /// Gets the current page number (1-based).
    /// </summary>
    public int Page => Take > 0 ? (Skip / Take) + 1 : 1;

    /// <summary>
    /// Gets the page size.
    /// </summary>
    public int PageSize => Take;
}
