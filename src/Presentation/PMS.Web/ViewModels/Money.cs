using System.Globalization;

namespace PMS.Web.ViewModels;

/// <summary>
/// Money on screen.
///
/// <para><b>This is where report figures round, and it is the only place they do.</b> The API
/// aggregates unrounded decimals all the way through — see <c>ProfitMath</c> — because rounding
/// per line and then summing drifts by up to a paisa a line, which over a month is a discrepancy
/// somebody will try to reconcile against a till and cannot. The last step before a person reads
/// the number is the right place to round it, and that step is here.</para>
///
/// <para>Invariant culture, deliberately. The server's culture is not the reader's, and a figure
/// that renders as <c>1.234,56</c> on one machine and <c>1,234.56</c> on another is a figure two
/// people will disagree about. Bangladesh writes money the second way.</para>
/// </summary>
public static class Money
{
    /// <summary>A money figure with a thousands separator and two decimals.</summary>
    public static string Format(decimal value) =>
        value.ToString("N2", CultureInfo.InvariantCulture);

    /// <summary>
    /// A money figure with its sign spelled out, for a column that can go either way.
    ///
    /// <para>A minus sign rendered as a hyphen is easy to miss on a profit column, and missing it
    /// inverts the meaning of the row. This uses a real minus sign, which is wider.</para>
    /// </summary>
    public static string Signed(decimal value) =>
        value < 0
            ? "−" + Math.Abs(value).ToString("N2", CultureInfo.InvariantCulture)
            : value.ToString("N2", CultureInfo.InvariantCulture);

    /// <summary>
    /// A per-unit cost, to four places.
    ///
    /// <para>Not two. A per-base-unit cost derived from a bulk pack is frequently fractional —
    /// a 30-tablet box at 500 is 16.6667 a tablet — and a column rounded to paisa would not
    /// reproduce the total beside it, which reads as an arithmetic error rather than as
    /// rounding.</para>
    /// </summary>
    public static string PerUnit(decimal value) =>
        value.ToString("N4", CultureInfo.InvariantCulture);

    /// <summary>A percentage to one decimal place, with its sign.</summary>
    public static string Percent(decimal value) =>
        value.ToString("0.0", CultureInfo.InvariantCulture) + "%";

    /// <summary>A plain count with a thousands separator.</summary>
    public static string Count(int value) =>
        value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>
    /// The CSS class for a figure that may be negative, or null for one that may not.
    ///
    /// <para>Colour is never the only signal — the minus sign from <see cref="Signed"/> carries
    /// the same information for a reader who cannot distinguish the two colours.</para>
    /// </summary>
    public static string? Tone(decimal value) => value switch
    {
        < 0 => "figure-negative",
        > 0 => "figure-positive",
        _ => null,
    };
}
