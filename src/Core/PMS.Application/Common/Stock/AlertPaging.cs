namespace PMS.Application.Common.Stock;

/// <summary>
/// Paging bounds for the alert lists.
///
/// <para>Here rather than repeated in three handlers, and clamped rather than validated: a
/// stale bookmark or a hand-edited query string should land on a sensible page, not a 400. An
/// alert list is something somebody glances at, and refusing to draw it because the page number
/// is 0 would be the wrong kind of correct.</para>
/// </summary>
public static class AlertPaging
{
    /// <summary>Matches every other grid in the product.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>
    /// An upper bound, because <c>pageSize</c> arrives from a query string. Without it, one
    /// request can ask for every batch in the pharmacy formatted into DTOs.
    /// </summary>
    public const int MaxPageSize = 200;

    public static int Page(int page) => page < 1 ? 1 : page;

    public static int Size(int pageSize) =>
        pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize;
}
