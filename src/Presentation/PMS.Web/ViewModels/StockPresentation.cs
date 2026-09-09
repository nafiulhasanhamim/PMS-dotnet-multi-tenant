using PMS.Web.Api;

namespace PMS.Web.ViewModels;

/// <summary>
/// How stock is phrased and coloured on screen.
///
/// <para><b>Nothing here computes a status.</b> Whether a product is low, or a batch expiring
/// soon, is decided by the API and arrives as an enum — this only chooses the words and the
/// CSS class for it. The alternative, deriving it from two numbers in a view, would put the
/// threshold in every page that renders stock and let them disagree.</para>
/// </summary>
public static class StockPresentation
{
    /// <summary>The status badge on the stock list and the product summary.</summary>
    public static (string Text, string Css) StatusBadge(StockStatus status) => status switch
    {
        StockStatus.OutOfStock => ("Out of stock", "pms-badge--danger"),
        StockStatus.Low => ("Low", "pms-badge--warning"),
        _ => ("OK", "pms-badge--neutral"),
    };

    /// <summary>
    /// The expiry cell: red once expired, amber inside the alert window, plain otherwise, and
    /// an em dash for stock that does not expire.
    ///
    /// <para>Never colour alone. Each returns a title for the cell as well, so the state is
    /// available to a screen reader and to anyone who cannot distinguish the two warm
    /// colours.</para>
    /// </summary>
    public static (string Text, string Css, string? Title) ExpiryCell(
        DateOnly? expiry, int? daysUntil, ExpiryState state)
    {
        if (expiry is null)
        {
            return ("—", "text-muted", "This product does not expire");
        }

        var date = expiry.Value.ToString("d MMM yyyy");

        return state switch
        {
            ExpiryState.Expired => (
                date,
                "pms-expiry--expired",
                daysUntil is { } d ? $"Expired {Math.Abs(d)} days ago" : "Expired"),

            ExpiryState.ExpiringSoon => (
                date,
                "pms-expiry--soon",
                daysUntil is { } d ? $"Expires in {d} days" : "Expiring soon"),

            _ => (date, string.Empty, daysUntil is { } d ? $"Expires in {d} days" : null),
        };
    }

    /// <summary>The label for an adjustment row, and its colour.</summary>
    public static (string Text, string Css) AdjustmentBadge(AdjustmentType type) => type switch
    {
        AdjustmentType.Add => ("Added", "pms-badge--success"),
        AdjustmentType.Remove => ("Removed", "pms-badge--danger"),
        _ => ("Correction", "pms-badge--neutral"),
    };

    /// <summary>
    /// The unit levels a product actually defines, as dropdown options.
    ///
    /// <para><b>Built from the product, never from a fixed list.</b> For Napa this yields
    /// piece, strip and box; for handwash, bottle and carton; for a saline bag, bag alone.
    /// Offering a level the product does not have would produce a quantity the server has to
    /// reject, and offering "strip" for a saline bag invites somebody to guess what one
    /// is.</para>
    /// </summary>
    public static IReadOnlyList<(UnitLevel Level, string Name)> UnitOptions(ProductModel product)
    {
        var options = new List<(UnitLevel, string)> { (UnitLevel.Base, product.BaseUnitName) };

        if (product.MidUnitName is { } mid)
        {
            options.Add((UnitLevel.Mid, mid));
        }

        if (product.LargeUnitName is { } large)
        {
            options.Add((UnitLevel.Large, large));
        }

        return options;
    }

    /// <summary>How many base units one of a level contains, for the live helper text.</summary>
    public static int BaseUnitsIn(UnitLevel level, ProductModel product) => level switch
    {
        UnitLevel.Mid => product.BasePerMid ?? 1,
        UnitLevel.Large => product.BaseUnitsPerLarge ?? 1,
        _ => 1,
    };

    /// <summary>
    /// The quick-pick reasons offered on the adjustment form.
    ///
    /// <para>Buttons that fill an editable field, not a dropdown that replaces it. A fixed
    /// list gets "Other" selected for exactly the cases worth reading later.</para>
    /// </summary>
    public static readonly string[] QuickReasons =
    [
        "Damage",
        "Breakage",
        "Counting correction",
        "Expired disposal",
        "Other",
    ];
}
