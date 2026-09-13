using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PMS.Web.ViewModels;

/// <summary>
/// Everything <c>_PriceGrid</c> needs, without knowing which screen it is on.
/// </summary>
/// <param name="FieldPrefix">
/// The bound collection name — <c>Rows</c> on both screens today. The grid builds
/// <c>Rows[3].PricePerBase</c> from it, which is what lets the same markup post into two
/// different page models.
/// </param>
public sealed record PriceGridViewModel(
    string Caption,
    string FieldPrefix,
    IReadOnlyList<PriceGridRow> Rows,
    IReadOnlyList<PriceGridColumn> PriceColumns,
    bool ShowAntibioticColumn,
    bool AllowUnitOverride,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Errors,
    IReadOnlyDictionary<string, decimal?> Values)
{
    public string FieldName(int index, string suffix) => $"{FieldPrefix}[{index}].{suffix}";

    public IReadOnlyList<string> ErrorsFor(string key) =>
        Errors.TryGetValue(key, out var messages) ? messages : [];

    /// <summary>
    /// The value to redisplay in a price box, so a failed save does not wipe what was typed.
    /// </summary>
    public decimal? PriceValue(string key, string suffix) =>
        Values.TryGetValue(key + "|" + suffix, out var value) ? value : null;

    /// <summary>
    /// Whether a row actually has the level a price column is for.
    ///
    /// <para>A product sold only in bags has no strip price to give, and rendering an input
    /// for one would send a value the server has to reject.</para>
    /// </summary>
    public bool RowHasLevel(PriceGridRow row, string suffix) => suffix switch
    {
        "PricePerMid" => row.MidUnitName is not null,
        "PricePerLarge" => row.LargeUnitName is not null,
        _ => true,
    };

    /// <summary>How the row is packed, in one short line — "10 pieces per strip, 10 per box".</summary>
    public string DescribeUnits(PriceGridRow row)
    {
        var parts = new List<string> { row.BaseUnitName };

        if (row.MidUnitName is not null)
        {
            parts.Add($"{row.BasePerMid ?? 0} per {row.MidUnitName}");
        }

        if (row.LargeUnitName is not null)
        {
            parts.Add($"{row.MidPerLarge ?? 0} per {row.LargeUnitName}");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// The hidden field carrying the row's identity.
    ///
    /// <para>Rendered by the grid rather than the page, so the two screens cannot disagree
    /// about the index the rest of the row is bound under — which would silently apply one
    /// product's price to another.</para>
    /// </summary>
    public IHtmlContent KeyField(int index, PriceGridRow row)
    {
        var tag = new TagBuilder("input");
        tag.TagRenderMode = TagRenderMode.SelfClosing;
        tag.Attributes["type"] = "hidden";
        tag.Attributes["name"] = FieldName(index, KeyFieldName);
        tag.Attributes["value"] = row.Key;

        return tag;
    }

    /// <summary>
    /// Which property the key binds to. <c>CatalogMedicineId</c> on the import setup screen,
    /// <c>ProductId</c> on the complete-setup screen.
    /// </summary>
    public string KeyFieldName { get; init; } = "CatalogMedicineId";
}

/// <param name="FieldSuffix">
/// The bound property — <c>PricePerBase</c>, <c>PricePerMid</c>, <c>PricePerLarge</c>. Also
/// the copy-down handle, so the script needs no separate mapping.
/// </param>
public sealed record PriceGridColumn(string Header, string FieldSuffix);
