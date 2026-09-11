namespace PMS.Application.Common.Antibiotics;

/// <summary>
/// The register's date range, resolved once.
///
/// <para>Shared by the page and the export so a printed CSV covers exactly what was on screen.
/// Two callers deriving "the current month" separately is how an export ends up one day wider
/// than the table it came from.</para>
/// </summary>
public static class RegisterRange
{
    /// <summary>
    /// Fills in whichever end was left out, and puts them the right way round.
    ///
    /// <para>Defaults to the current month, which is what a register is read for: what have we
    /// dispensed this month. Swapping a reversed range rather than refusing it, because a person
    /// who types the dates the wrong way round wants to see the rows, not a validation
    /// message.</para>
    /// </summary>
    public static (DateOnly From, DateOnly To) Resolve(
        DateOnly? from, DateOnly? to, DateOnly today)
    {
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today;

        return start <= end ? (start, end) : (end, start);
    }
}
