namespace PMS.Web.Api;

/// <summary>
/// One page of rows, plus what the pagination control needs to render itself.
///
/// **Read this before reusing it.** The API currently returns whole lists — neither
/// `GET /api/platform/tenants` nor `GET /api/users` takes a page parameter — so the slicing
/// here happens in this app, after the full list has crossed the wire. The controls and the
/// "Showing X–Y of Z" line are therefore honest about what is on screen, but they are not
/// saving the API or the database any work.
///
/// That is fine at Module 1 scale (a pharmacy has tens of staff, the platform has tens of
/// pharmacies) and wrong at any real scale. When the API grows page/pageSize parameters, the
/// fix is to pass them through and build this from the response's own total — every call site
/// and every view keeps working, because they only ever read the properties below.
/// </summary>
public sealed class PagedView<T>
{
    public const int DefaultPageSize = 25;

    private PagedView(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }

    public IReadOnlyList<T> Items { get; }

    public int TotalCount { get; }

    public int Page { get; }

    public int PageSize { get; }

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    /// <summary>1-based index of the first row on this page, or 0 when there are none.</summary>
    public int FirstRow => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastRow => Math.Min(Page * PageSize, TotalCount);

    public bool IsEmpty => TotalCount == 0;

    /// <summary>
    /// Takes one page out of a full list, clamping the requested page into range so a stale
    /// bookmark or a hand-edited query string shows the last page rather than nothing.
    /// </summary>
    public static PagedView<T> From(
        IReadOnlyList<T>? source, int page, int pageSize = DefaultPageSize)
    {
        var all = source ?? Array.Empty<T>();
        var size = pageSize < 1 ? DefaultPageSize : pageSize;
        var totalPages = all.Count == 0 ? 1 : (int)Math.Ceiling(all.Count / (double)size);
        var current = Math.Clamp(page < 1 ? 1 : page, 1, totalPages);

        var items = all.Skip((current - 1) * size).Take(size).ToList();

        return new PagedView<T>(items, all.Count, current, size);
    }
}
