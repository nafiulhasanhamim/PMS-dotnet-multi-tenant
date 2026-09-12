namespace PMS.Application.Common.Salary;

/// <summary>
/// Page sizes for this module, clamped in one place.
///
/// <para>The brief specifies 25 for all four paginated lists, so unlike Module 4 there is no
/// second size here. Hard-coding the number at each of the four call sites is how one of them
/// silently becomes something else.</para>
///
/// <para>Clamped rather than trusted: a hand-edited <c>pageSize=100000</c> is a request to read a
/// pharmacy's entire salary history into memory, and refusing it with a validation error would be
/// worse than quietly serving a sane page.</para>
/// </summary>
public static class SalaryPaging
{
    public const int DefaultPageSize = 25;

    public const int MaxPageSize = 200;

    public static int Page(int page) => page < 1 ? 1 : page;

    public static int Size(int pageSize) =>
        pageSize switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => pageSize,
        };
}
