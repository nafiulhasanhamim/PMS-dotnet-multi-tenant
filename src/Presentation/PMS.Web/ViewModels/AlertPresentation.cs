using PMS.Web.Api;

namespace PMS.Web.ViewModels;

/// <summary>
/// How an alert row looks and what it offers to do next.
///
/// <para>The severity itself is decided server-side — see <c>StockPolicy.SeverityFor</c> — so
/// that the API, the list and the dashboard agree on what counts as urgent. What lives here is
/// only the mapping from that decision to CSS and to wording.</para>
/// </summary>
public static class AlertPresentation
{
    /// <summary>Row styling for an expiry row.</summary>
    public static string RowCss(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => "pms-row-danger",
        AlertSeverity.Warning => "pms-row-warning",
        _ => string.Empty,
    };

    /// <summary>
    /// "12 days", "1 day", "Today". Used on the expiring list, where the number is the whole
    /// reason somebody is reading the row.
    /// </summary>
    public static string DaysRemaining(int days) => days switch
    {
        <= 0 => "Today",
        _ => Copy.Count(days, "day"),
    };

    /// <summary>"12 days overdue", "1 day overdue".</summary>
    public static string DaysOverdue(int daysOverdue) =>
        $"{Copy.Count(daysOverdue, "day")} overdue";

    /// <summary>
    /// What the expired page offers as the next action for one batch.
    ///
    /// <para><b>The choice is made from whether the delivery was recorded against a supplier.</b>
    /// Stock that came in on a purchase can go back on one; stock somebody typed in by hand has
    /// nobody to return it to, and the only honest thing to offer is a write-off through a stock
    /// adjustment.</para>
    ///
    /// <para><b>Both branches point at Adjust stock today, and that is a gap rather than a
    /// decision.</b> The return-to-supplier screen belongs to Module 4, which is not built — no
    /// Supplier entity exists, so no batch can currently carry a real purchase behind its
    /// supplier id either. The branch is written out so that wiring it up is a one-line change
    /// rather than an archaeology exercise; see the frontend doc.</para>
    /// </summary>
    /// <summary>
    /// What the expired-stock page offers for a batch.
    ///
    /// <para><b>Takes the purchase origin rather than reading the batch's supplier id.</b> Those
    /// were the same question until Module 4: now Add Stock can name a supplier on a batch entered
    /// by hand, and such a batch has a supplier but no bill to send anything back against. Asking
    /// whether a purchase line references the batch is the only version of this that stays
    /// true.</para>
    /// </summary>
    public static ExpiredAction ActionFor(PurchaseOrigin? origin) =>
        origin is not null
            ? ExpiredAction.ReturnToSupplier
            : ExpiredAction.AdjustStock;
}

/// <summary>What to do with a batch that has expired.</summary>
public enum ExpiredAction
{
    /// <summary>Write it off through a stock adjustment.</summary>
    AdjustStock = 0,

    /// <summary>Hand it back on the purchase it arrived on.</summary>
    ReturnToSupplier = 1,
}
