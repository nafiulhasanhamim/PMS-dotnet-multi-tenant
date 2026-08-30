namespace PMS.SharedKernel.Grid;

/// <summary>
/// Represents the result of a grid data request with pagination metadata.
/// Compatible with Telerik/Kendo UI grid data source format.
/// </summary>
/// <typeparam name="T">The type of items in the result.</typeparam>
public class GridResult<T>
{
    /// <summary>
    /// Gets or sets the data items for the current page.
    /// </summary>
    public IEnumerable<T> Data { get; set; } = [];

    /// <summary>
    /// Gets or sets the total count of items (before pagination).
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Gets the current page number (1-based).
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets the page size.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)Total / PageSize) : 0;

    /// <summary>
    /// Gets a value indicating whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Gets a value indicating whether there is a next page.
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Creates an empty grid result.
    /// </summary>
    public static GridResult<T> Empty => new() { Data = [], Total = 0 };

    /// <summary>
    /// Creates a grid result from a collection.
    /// </summary>
    /// <param name="data">The data items.</param>
    /// <param name="total">The total count.</param>
    /// <param name="page">The current page.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>A new GridResult instance.</returns>
    public static GridResult<T> Create(IEnumerable<T> data, int total, int page = 1, int pageSize = 20)
    {
        return new GridResult<T>
        {
            Data = data,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
