namespace PMS.Application.Common.Purchasing;

/// <summary>
/// Page sizes for this module, clamped in one place.
///
/// <para>The brief specifies 25 for the supplier and purchase lists and 20 for the two history
/// tables on a supplier's page. Those are different numbers for a reason — the history tables sit
/// two to a screen — and hard-coding them at each call site is how one of them silently becomes
/// the other.</para>
///
/// <para>Clamped rather than trusted: a hand-edited <c>pageSize=100000</c> is a request to read a
/// pharmacy's whole purchase history into memory, and refusing it with a validation error would be
/// worse than quietly serving a sane page.</para>
/// </summary>
public static class SupplierPaging
{
    public const int DefaultPageSize = 25;

    /// <summary>The supplier detail page's purchase and payment tables.</summary>
    public const int DefaultHistoryPageSize = 20;

    public const int MaxPageSize = 200;

    public static int Page(int page) => page < 1 ? 1 : page;

    public static int Size(int pageSize) =>
        pageSize switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => pageSize,
        };

    public static int HistorySize(int pageSize) =>
        pageSize switch
        {
            < 1 => DefaultHistoryPageSize,
            > MaxPageSize => MaxPageSize,
            _ => pageSize,
        };
}
