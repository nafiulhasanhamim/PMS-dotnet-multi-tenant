using PMS.Web.Api;

namespace PMS.Web.ViewModels;

/// <summary>
/// One row of a price grid, shared by the bulk setup screen and the complete-setup screen.
///
/// <para>The two screens are the same problem at different moments — a list of products and
/// the prices they still need — so they render the same partial over this shape. The
/// difference is only where the rows come from: a catalogue selection in one case, the
/// pharmacy's own incomplete products in the other.</para>
/// </summary>
/// <param name="Key">
/// Identifies the row in the posted form. A catalogue id on the setup screen, a product id on
/// the complete-setup screen — the partial does not care which, and the page model that binds
/// the form knows.
/// </param>
/// <param name="IsAntibioticSuggested">
/// True when the row was flagged by the catalogue's classifier. Drives the amber tint: the
/// point is to draw the pharmacist's eye to the classifications that need confirming, not to
/// mark every antibiotic.
/// </param>
public sealed record PriceGridRow(
    string Key,
    string BrandName,
    string? GenericName,
    string? Strength,
    bool IsAntibioticSuggested,
    string BaseUnitName,
    string? MidUnitName,
    string? LargeUnitName,
    int? BasePerMid,
    int? MidPerLarge);

/// <summary>The unit shapes offered by the shared preset control.</summary>
public enum UnitPresetKind
{
    /// <summary>Piece, strip, box — the default, because most bulk-imported items are tablets.</summary>
    PieceStripBox = 0,

    /// <summary>A base unit and a bulk pack, no middle level.</summary>
    UnitAndBulk = 1,

    /// <summary>One level only.</summary>
    SingleUnit = 2,
}

/// <summary>What each preset means, in one place, so the pages and the JavaScript agree.</summary>
public static class UnitPresets
{
    public static (string Base, string? Mid, string? Large, int? BasePerMid, int? MidPerLarge)
        Shape(UnitPresetKind kind) => kind switch
    {
        UnitPresetKind.PieceStripBox => ("piece", "strip", "box", 10, 10),

        // MidPerLarge counts BASE units when there is no middle level — 24 bottles per carton,
        // not 24 strips. The single most error-prone number in the system; see Product.
        UnitPresetKind.UnitAndBulk => ("bottle", null, "carton", null, 24),

        _ => ("piece", null, null, null, null),
    };

    public static string Describe(UnitPresetKind kind) => kind switch
    {
        UnitPresetKind.PieceStripBox => "Piece → strip → box (standard tablet)",
        UnitPresetKind.UnitAndBulk => "Unit + bulk pack",
        _ => "Single unit only",
    };
}
