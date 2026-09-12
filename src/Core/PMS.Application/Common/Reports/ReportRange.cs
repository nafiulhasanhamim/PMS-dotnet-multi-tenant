namespace PMS.Application.Common.Reports;

/// <summary>
/// A report's date range, resolved once.
///
/// <para>Shared by every ranged report and by its export, so a downloaded CSV covers exactly the
/// period that was on screen. Two callers each working out "the last thirty days" independently is
/// how an export ends up a day wider than the table it came from.</para>
///
/// <para>The same shape as <c>RegisterRange</c>, with a different default: the antibiotic register
/// is read as "what have we dispensed this month", while a sales report is read as "how are we
/// doing lately", which is a rolling window rather than a calendar one. They are kept separate
/// rather than parameterised because the two defaults are answers to two different questions, and
/// merging them would mean one of the two screens silently changing if the other's default were
/// ever revisited.</para>
/// </summary>
public static class ReportRange
{
    /// <summary>
    /// How far back an unspecified range reaches. Thirty days is long enough to show a trend and
    /// short enough that the first page is fast on a busy pharmacy's data.
    /// </summary>
    public const int DefaultDays = 30;

    /// <summary>
    /// Fills in whichever end was left out, and puts them the right way round.
    ///
    /// <para>A reversed range is swapped rather than refused: somebody who typed the dates the
    /// wrong way round wants to see the rows, not a validation message telling them what they can
    /// already see.</para>
    /// </summary>
    public static (DateOnly From, DateOnly To) Resolve(
        DateOnly? from, DateOnly? to, DateOnly today)
    {
        var end = to ?? today;

        // Inclusive of both ends, so the default window is DefaultDays days long rather than
        // DefaultDays + 1.
        var start = from ?? end.AddDays(-(DefaultDays - 1));

        return start <= end ? (start, end) : (end, start);
    }
}
