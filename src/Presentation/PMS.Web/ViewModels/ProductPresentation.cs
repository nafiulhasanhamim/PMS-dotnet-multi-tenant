using PMS.Web.Api;

namespace PMS.Web.ViewModels;

/// <summary>
/// How a product is shown. One place, so the two list screens, the detail page and the form
/// all label the same thing the same way.
/// </summary>
public static class ProductPresentation
{
    /// <summary>Sentence-case labels for the type dropdown and badges.</summary>
    public static string TypeLabel(ProductType type) => type switch
    {
        ProductType.Medicine => "Medicine",
        ProductType.MedicalSupply => "Medical supply",
        ProductType.BabyCare => "Baby care",
        ProductType.PersonalCare => "Personal care",
        ProductType.Supplement => "Supplement",
        _ => "Other",
    };

    public static (string Css, string Text) TypeBadge(ProductType type) => type switch
    {
        ProductType.Medicine => ("pms-badge--info", "Medicine"),
        ProductType.MedicalSupply => ("pms-badge--neutral", "Medical supply"),
        ProductType.BabyCare => ("pms-badge--neutral", "Baby care"),
        ProductType.PersonalCare => ("pms-badge--neutral", "Personal care"),
        ProductType.Supplement => ("pms-badge--neutral", "Supplement"),
        _ => ("pms-badge--neutral", "Other"),
    };

    public static (string Css, string Text) StatusBadge(bool isActive) =>
        isActive ? ("pms-badge--success", "Active") : ("pms-badge--neutral", "Inactive");

    /// <summary>
    /// Every type a pharmacy can pick on the "other items" form.
    ///
    /// Medicine is absent on purpose: that form does not render generic name, strength or
    /// dosage form, so choosing Medicine there would produce a medicine the API then rejects
    /// for missing them.
    /// </summary>
    public static IReadOnlyList<ProductType> NonMedicineTypes { get; } =
    [
        ProductType.MedicalSupply,
        ProductType.BabyCare,
        ProductType.PersonalCare,
        ProductType.Supplement,
        ProductType.Other,
    ];

    /// <summary>
    /// A one-line description of how a product is packed, mirroring the server's
    /// <c>UnitConversion.DescribePacking</c> — "1 box = 10 strips = 100 pieces",
    /// "1 carton = 24 bottles", "Sold as individual bags only".
    ///
    /// <para>Duplicated here rather than shared, because this project holds no reference to
    /// the Application layer. The detail page uses the server's own <c>PackingSummary</c>;
    /// this exists for the form's live preview, where there is no server round trip to ask.
    /// The two must agree, and the base-units-per-large rule below is the part to keep in
    /// step.</para>
    /// </summary>
    public static string DescribePacking(
        string? baseUnit, string? midUnit, string? largeUnit, int? basePerMid, int? midPerLarge)
    {
        var bu = (baseUnit ?? string.Empty).Trim();

        if (bu.Length == 0)
        {
            return string.Empty;
        }

        var hasMid = !string.IsNullOrWhiteSpace(midUnit);
        var hasLarge = !string.IsNullOrWhiteSpace(largeUnit);
        var basePlural = Pluralise(bu, 2);

        if (!hasMid && !hasLarge)
        {
            return $"Sold as individual {basePlural} only";
        }

        var parts = new List<string>();

        if (hasLarge && midPerLarge is > 0)
        {
            // The trap: without a mid level, MidPerLarge already counts BASE units, so it
            // must not be multiplied by anything.
            var perLarge = hasMid ? (basePerMid ?? 0) * midPerLarge.Value : midPerLarge.Value;

            if (hasMid && basePerMid is not > 0)
            {
                // Mid named but its count not entered yet — say what is known, not a wrong sum.
                return $"1 {largeUnit!.Trim()} = {midPerLarge} {Pluralise(midUnit!.Trim(), midPerLarge.Value)}";
            }

            parts.Add($"1 {largeUnit!.Trim()}");

            if (hasMid)
            {
                parts.Add($"{midPerLarge} {Pluralise(midUnit!.Trim(), midPerLarge.Value)}");
            }

            parts.Add($"{perLarge} {basePlural}");
        }
        else if (hasMid && basePerMid is > 0)
        {
            parts.Add($"1 {midUnit!.Trim()}");
            parts.Add($"{basePerMid} {basePlural}");
        }

        return parts.Count > 0 ? string.Join(" = ", parts) : string.Empty;
    }

    /// <summary>
    /// The plural of a unit name, for labels like "Reorder level (in bottles)".
    /// </summary>
    public static string PluralUnit(string? unitName) =>
        string.IsNullOrWhiteSpace(unitName) ? "units" : Pluralise(unitName.Trim(), 2);

    /// <summary>
    /// Naive English pluralisation, matching the server's. Enough for the unit names in use:
    /// piece, strip, box, bottle, carton, tin, bag, pack, sachet, vial, tube.
    /// </summary>
    private static string Pluralise(string noun, int count)
    {
        if (count == 1 || string.IsNullOrWhiteSpace(noun))
        {
            return noun;
        }

        var lower = noun.ToLowerInvariant();

        if (lower.EndsWith('s') || lower.EndsWith('x') || lower.EndsWith('z')
            || lower.EndsWith("ch") || lower.EndsWith("sh"))
        {
            return noun + "es";
        }

        if (lower.Length > 1 && lower.EndsWith('y') && !"aeiou".Contains(lower[^2]))
        {
            return noun[..^1] + "ies";
        }

        return noun + "s";
    }
}
