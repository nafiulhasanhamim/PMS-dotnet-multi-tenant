using PMS.Web.Api;

namespace PMS.Web.ViewModels;

/// <summary>
/// One alert card. The dashboard renders four small ones, the hub four large ones, and both
/// take their wording and their colour from here so the two screens cannot drift.
/// </summary>
/// <param name="Tone">
/// Amber or red <b>only when the count is non-zero</b>. A pharmacy with nothing expiring and
/// nothing running out should not open its dashboard to four warning-coloured boxes: colour that
/// is always on stops meaning anything, and the first thing a person learns is to ignore it.
/// </param>
public sealed record AlertCardModel(
    string Title,
    int Count,
    string Description,
    string Page,
    AlertTone Tone,
    string? Preview = null)
{
    public bool IsQuiet => Count == 0;

    /// <summary>The card's CSS, calm when there is nothing to report.</summary>
    public string Css => IsQuiet
        ? "pms-alert-card pms-alert-card--quiet"
        : Tone switch
        {
            AlertTone.Danger => "pms-alert-card pms-alert-card--danger",
            AlertTone.Warning => "pms-alert-card pms-alert-card--warning",
            _ => "pms-alert-card",
        };

    /// <summary>
    /// The four cards, built from one summary. Built here rather than in each Razor page so the
    /// dashboard and the hub say the same things in the same order.
    /// </summary>
    public static IReadOnlyList<AlertCardModel> From(AlertSummary summary) =>
    [
        new AlertCardModel(
            "Expiring soon",
            summary.ExpiringSoon,
            $"Batches with stock that expire within {summary.ExpiryWindowDays} days.",
            "/Alerts/Expiring",
            AlertTone.Warning,
            summary.NearestExpiry is { } nearest
                ? $"Nearest: {nearest.BrandName} ({nearest.BatchNumber}) "
                  + $"\u2014 {Copy.Count(nearest.DaysUntilExpiry, "day")}"
                : null),

        new AlertCardModel(
            "Expired",
            summary.Expired,
            "Batches past their expiry date that are still on the shelf. These are blocked "
            + "from sale.",
            "/Alerts/Expired",
            AlertTone.Danger),

        new AlertCardModel(
            "Low stock",
            summary.LowStock,
            "Products at or below their reorder level, with some stock left.",
            "/Alerts/LowStock",
            AlertTone.Warning),

        new AlertCardModel(
            "Out of stock",
            summary.OutOfStock,
            "Products with nothing left to sell.",
            "/Alerts/LowStock",
            AlertTone.Danger),
    ];
}

public enum AlertTone
{
    Neutral = 0,
    Warning = 1,
    Danger = 2,
}
