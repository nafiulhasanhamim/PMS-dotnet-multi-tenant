using PMS.Application.Common.DTOs;

namespace PMS.Application.Common.Stock;

/// <summary>
/// The rules this module judges stock by — the shape of them, not the numbers.
///
/// <para><b>Module 10 took the two configurable numbers out of this class.</b> The expiry window
/// and the dead-stock threshold belong to the pharmacy — a shop turning over paracetamol weekly
/// wants a different horizon from one stocking slow-moving surgical supplies — and they now come
/// from <c>ISettingsService</c>, with their fallback defaults declared once in
/// <c>SettingKeys</c>. Gathering them here first is what made that a small change: the call
/// sites already passed the value as a parameter.</para>
///
/// <para><b>What is still constant here is deliberate.</b> The amber and red thresholds on an
/// expiry row, and the point at which adding stock to a nearly-expired batch asks for
/// confirmation, are judgements about how to <em>present</em> a risk rather than how much risk a
/// pharmacy will carry. Module 10's brief is explicit that settings not in its table should not
/// be invented, and these would be three more knobs nobody asked for.</para>
/// </summary>
public static class StockPolicy
{
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
    /// The windows the expiring-soon page offers regardless of what the pharmacy has configured.
    ///
    /// <para>Here rather than in the Razor page so the API and the screen cannot disagree about
    /// what is selectable.</para>
    /// </summary>
    public static readonly IReadOnlyList<int> StandardExpiryWindows = [30, 60, 90, 180];

    /// <summary>
    /// The windows a caller may ask for, given this pharmacy's configured one.
    ///
    /// <para><b>The configured window is always offered</b>, even when it is not one of the four
    /// standard choices. A pharmacy that set 45 days and then found the dropdown could not show
    /// 45 would have a settings screen that its own alert page disagreed with.</para>
    /// </summary>
    public static IReadOnlyList<int> SelectableExpiryWindows(int configuredWindowDays) =>
        StandardExpiryWindows.Contains(configuredWindowDays)
            ? StandardExpiryWindows
            : StandardExpiryWindows.Append(configuredWindowDays).Order().ToList();

    /// <summary>
    /// Whether <paramref name="days"/> is a window a caller may ask for, falling back to the
    /// pharmacy's configured window.
    ///
    /// <para>Bounded rather than free-form: an unbounded <c>days</c> parameter is a request for
    /// every batch in the pharmacy dressed up as an alert query.</para>
    /// </summary>
    /// <param name="configuredWindowDays">
    /// From <c>ISettingsService</c>, key <c>expiry_alert_window_days</c>. Passed in rather than
    /// read here so this class stays a pure function of its inputs and the settings read happens
    /// once per request at the handler.
    /// </param>
    public static int CoerceExpiryWindow(int? days, int configuredWindowDays) =>
        days is { } value && SelectableExpiryWindows(configuredWindowDays).Contains(value)
            ? value
            : configuredWindowDays;

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

    /// <summary>The thresholds the dead-stock report offers regardless of configuration.</summary>
    public static readonly IReadOnlyList<int> StandardDeadStockThresholds = [30, 60, 90, 180];

    /// <summary>
    /// The dead-stock thresholds a caller may ask for, given this pharmacy's configured one.
    /// Always includes the configured value, for the same reason the expiry windows do.
    /// </summary>
    public static IReadOnlyList<int> SelectableDeadStockThresholds(int configuredThresholdDays) =>
        StandardDeadStockThresholds.Contains(configuredThresholdDays)
            ? StandardDeadStockThresholds
            : StandardDeadStockThresholds.Append(configuredThresholdDays).Order().ToList();

    /// <summary>
    /// Whether <paramref name="days"/> is a dead-stock threshold a caller may ask for. Bounded
    /// for the same reason the expiry window is: an unbounded value turns a report into a request
    /// for the whole catalogue.
    /// </summary>
    /// <param name="configuredThresholdDays">
    /// From <c>ISettingsService</c>, key <c>dead_stock_threshold_days</c>.
    /// </param>
    public static int CoerceDeadStockThreshold(int? days, int configuredThresholdDays) =>
        days is { } value
        && SelectableDeadStockThresholds(configuredThresholdDays).Contains(value)
            ? value
            : configuredThresholdDays;

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
