using System.Globalization;

namespace PMS.Web.ViewModels;

/// <summary>One bar or one point.</summary>
/// <param name="Label">What goes under the axis. Kept short — these are drawn at 10px.</param>
/// <param name="Value">The magnitude. May be negative on a profit series.</param>
/// <param name="Detail">The tooltip line, already formatted.</param>
public sealed record ChartPoint(string Label, decimal Value, string Detail);

/// <summary>
/// A chart, rendered as inline SVG on the server.
///
/// <para><b>No charting library, and that is a decision rather than an omission.</b> Nothing in
/// this stack draws charts today; adding Chart.js for eight report pages would mean a new
/// dependency, a new bundle on every page that loads it, and a blank rectangle for anyone whose
/// script fails. Server-rendered SVG needs none of that: it is in the HTML the page already sent,
/// it prints — which matters, because these are reports people print — it scales without
/// blurring, and it inherits the theme's colours through <c>currentColor</c> and CSS variables.
/// A future need for zooming or live updating would justify revisiting it; reading last month's
/// sales does not.</para>
///
/// <para>The chart is always accompanied by the table it summarises. It is an aid to seeing a
/// shape, never the only way to reach a number, so a reader using a screen reader loses nothing:
/// the SVG is <c>aria-hidden</c> and the table beside it carries the data.</para>
/// </summary>
public sealed class ChartModel
{
    public required IReadOnlyList<ChartPoint> Points { get; init; }

    /// <summary>Drawing width in user units. The SVG scales to its container.</summary>
    public int Width { get; init; } = 720;

    public int Height { get; init; } = 180;

    /// <summary>Label every nth point, so a 31-day axis does not turn into a smear.</summary>
    public int LabelEvery { get; init; } = 1;

    /// <summary>Sets the fill; one of the <c>chart-*</c> classes in the stylesheet.</summary>
    public string Tone { get; init; } = "chart-primary";

    /// <summary>Shown in place of the chart when there is nothing to draw.</summary>
    public string EmptyMessage { get; init; } = "Nothing to chart for this period.";

    public bool IsEmpty => Points.Count == 0 || Points.All(p => p.Value == 0m);

    /// <summary>
    /// The largest magnitude in the series, never zero.
    ///
    /// <para>Magnitude, not maximum, because a profit series can be negative and a bar scaled
    /// against a negative maximum would point the wrong way. Never zero because it is a
    /// divisor.</para>
    /// </summary>
    public decimal Scale
    {
        get
        {
            var largest = Points.Count == 0 ? 0m : Points.Max(p => Math.Abs(p.Value));

            return largest == 0m ? 1m : largest;
        }
    }

    /// <summary>A value as a fraction of the scale, 0 to 1.</summary>
    public double Fraction(decimal value) => (double)(Math.Abs(value) / Scale);

    /// <summary>SVG needs invariant decimals; a comma separator would break the path.</summary>
    public static string Coord(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
