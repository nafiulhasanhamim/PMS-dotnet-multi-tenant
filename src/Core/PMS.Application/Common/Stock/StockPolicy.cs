using PMS.Application.Common.DTOs;

namespace PMS.Application.Common.Stock;

/// <summary>
/// The numbers this module judges stock by.
///
/// <para><b>Constants for now, and named as one place on purpose.</b> All of these belong to
/// the pharmacy — a shop turning over paracetamol weekly wants a different expiry horizon from
/// one stocking slow-moving supplies — and they move to per-tenant Settings in a later
/// module.
/// Gathering them here means that change edits one file and a handful of call sites that
/// already pass the value as a parameter, rather than hunting literal 90s through queries,
/// pages and alerts.</para>
/// </summary>
public static class StockPolicy
{
    /// <summary>
    /// How many days ahead counts as "expiring soon": the amber window on every screen and the
    /// basis of the expiry filter.
    ///
    /// <para>Ninety days because that is roughly the point at which a pharmacy can still act —
    /// return it to the supplier, discount it, or stop reordering. A shorter window tells them
    /// only after it is too late to do anything but write it off.</para>
    /// </summary>
    public const int ExpiringSoonWindowDays = 90;

    /// <summary>
    /// Days remaining at which an expiry row turns red.
    ///
    /// <para>A week is about the point at which returning stock to a supplier stops being
    /// realistic and the choice narrows to selling it fast or writing it off. Rows inside this
    /// threshold are the ones somebody has to look at today.</para>
    /// </summary>
    public const int ExpiryCriticalDays = 7;

    /// <summary>
    /// Days remaining at which an expiry row turns amber.
    ///
    /// <para>Equal to <see cref="AddToExpiringBatchWarningDays"/> today, and deliberately a
    /// separate name: one governs a colour on a list, the other governs a confirmation on a
    /// form. They answer to the same instinct about a month being the last comfortable moment
    /// to act, and there is no reason a pharmacy tuning one must move the other.</para>
    /// </summary>
    public const int ExpiryWarningDays = 30;

    /// <summary>
    /// The windows the expiring-soon page offers in its dropdown.
    ///
    /// <para>Here rather than in the Razor page so that the API and the screen cannot disagree
    /// about what is selectable, and so the set moves to Settings with everything else.</para>
    /// </summary>
    public static readonly IReadOnlyList<int> SelectableExpiryWindows = [30, 60, 90, 180];

    /// <summary>
    /// Whether <paramref name="days"/> is a window a caller may ask for.
    ///
    /// <para>Bounded rather than free-form: an unbounded <c>days</c> parameter is a request for
    /// every batch in the pharmacy dressed up as an alert query, and the page has four fixed
    /// choices anyway.</para>
    /// </summary>
    public static int CoerceExpiryWindow(int? days) =>
        days is { } value && SelectableExpiryWindows.Contains(value)
            ? value
            : ExpiringSoonWindowDays;

    /// <summary>
    /// How urgent a row is, from the days left on it. Negative days are already expired, and
    /// nothing is more urgent than that.
    /// </summary>
    public static AlertSeverity SeverityFor(int daysUntilExpiry) => daysUntilExpiry switch
    {
        <= ExpiryCriticalDays => AlertSeverity.Critical,
        <= ExpiryWarningDays => AlertSeverity.Warning,
        _ => AlertSeverity.Normal,
    };

    /// <summary>
    /// How close to expiry a batch has to be before <em>adding</em> stock to it is treated as
    /// probably a mistake.
    ///
    /// <para>Much shorter than the alert window, and deliberately. Adding to a batch that
    /// expires in two months is ordinary — a miscount corrected. Adding to one that expires
    /// this month almost always means fresh stock arrived and somebody reached for the nearest
    /// existing row instead of creating a new batch, which silently gives the new delivery the
    /// old one's expiry date and cost.</para>
    /// </summary>
    public const int AddToExpiringBatchWarningDays = 30;
}
