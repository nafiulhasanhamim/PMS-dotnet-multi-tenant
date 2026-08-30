namespace PMS.SharedKernel.Grid;

/// <summary>
/// Describes a filter to apply to grid data.
/// </summary>
public class FilterDescriptor
{
    /// <summary>
    /// Gets or sets the field/property name to filter on.
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the filter operator (eq, neq, contains, startswith, endswith, gt, gte, lt, lte, isnull, isnotnull).
    /// </summary>
    public string Operator { get; set; } = "eq";

    /// <summary>
    /// Gets or sets the value to filter by.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Gets or sets the logical operator to use with other filters (and, or).
    /// </summary>
    public string Logic { get; set; } = "and";

    /// <summary>
    /// Gets or sets nested filters for complex filter expressions.
    /// </summary>
    public List<FilterDescriptor>? Filters { get; set; }
}
