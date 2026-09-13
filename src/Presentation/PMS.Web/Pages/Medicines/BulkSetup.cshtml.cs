using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.Selection;
using PMS.Web.ViewModels;

namespace PMS.Web.Pages.Medicines;

/// <summary>
/// Sets up everything selected on the import screen, in one grid.
///
/// <para>Three sections, in the order the decisions are actually made: the unit shape that
/// applies to nearly every row, then the per-row review, then how much of it to commit.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantWriter)]
public class BulkSetupModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public BulkSetupModel(PmsApiClient api)
    {
        _api = api;
    }

    /// <summary>
    /// The unit shape applied to every row, unless a row overrides it.
    ///
    /// <para>Defaults to piece/strip/box because most of what a pharmacy bulk-imports is
    /// tablets. A default that fits the common case is the difference between confirming one
    /// dropdown and filling in five fields fifty times.</para>
    /// </summary>
    [BindProperty]
    public UnitPresetKind SharedPreset { get; set; } = UnitPresetKind.PieceStripBox;

    [BindProperty]
    public List<BulkSetupRowInput> Rows { get; set; } = [];

    /// <summary>The catalogue entries behind the rows, for the read-only columns.</summary>
    public IReadOnlyDictionary<int, CatalogMedicineSearchItem> Entries { get; private set; }
        = new Dictionary<int, CatalogMedicineSearchItem>();

    /// <summary>Row-level failures from the API, keyed by catalogue id.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<string>> RowErrors { get; private set; }
        = new Dictionary<int, IReadOnlyList<string>>();

    /// <summary>
    /// The failures as a list to render above the grid: which medicine, and why.
    ///
    /// <para>The rows are marked in place as well, but "they are marked below" is not much
    /// help when there are twenty of them and two are wrong — the person has to scroll and
    /// hunt. Naming them here means the banner answers the question it raises.</para>
    /// </summary>
    public IReadOnlyList<(string Name, string Reason)> FailureSummary { get; private set; } = [];

    public int SelectedCount => Rows.Count;

    /// <summary>How many rows still have a price missing. Drives the save buttons.</summary>
    public int MissingPriceCount => Rows.Count(row => !row.HasAllPrices);

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var selection = HttpContext.Session.GetCatalogSelection();

        if (selection.Count == 0)
        {
            // Nothing selected: there is no meaningful empty state for this screen, so send
            // them back to the one place where a selection can be made.
            return RedirectToPage("/Medicines/Import");
        }

        var redirect = await LoadEntriesAsync(selection.Ids, ct);
        if (redirect is not null)
        {
            return redirect;
        }

        var settings = await _api.GetSettingsAsync(ct);
        var reorderLevel = (settings.Value ?? TenantSettings.Fallback).DefaultReorderLevel;

        Rows = Entries.Values
            .OrderBy(entry => entry.BrandName)
            .Select(entry => new BulkSetupRowInput
            {
                CatalogMedicineId = entry.Id,

                // Pre-set from the catalogue, and editable. The catalogue's flag is
                // machine-derived; this is the screen where a pharmacist confirms it.
                IsAntibiotic = entry.IsAntibiotic,
                UsesSharedUnits = true,

                // The pharmacy's own starting value since Module 10, on every row. Setting two
                // hundred medicines up at once is exactly when a sensible default earns its
                // keep - and exactly when nobody wants to type the same number two hundred
                // times.
                ReorderLevel = reorderLevel,
            })
            .ToList();

        ApplyPresetToRows();

        return Page();
    }

    /// <summary>Re-renders after the shared preset changes, without losing typed prices.</summary>
    public async Task<IActionResult> OnPostApplyPresetAsync(CancellationToken ct)
    {
        var selection = HttpContext.Session.GetCatalogSelection();

        var redirect = await LoadEntriesAsync(selection.Ids, ct);
        if (redirect is not null)
        {
            return redirect;
        }

        ApplyPresetToRows();

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(bool withoutPrices, CancellationToken ct)
    {
        var selection = HttpContext.Session.GetCatalogSelection();

        var redirect = await LoadEntriesAsync(selection.Ids, ct);
        if (redirect is not null)
        {
            return redirect;
        }

        if (Rows.Count == 0)
        {
            return RedirectToPage("/Medicines/Import");
        }

        if (!withoutPrices && MissingPriceCount > 0)
        {
            // The button is disabled client-side, so reaching here means JavaScript is off or
            // somebody posted directly. Same answer either way.
            ShowError(
                $"{MissingPriceCount} of {Rows.Count} "
                + $"{Copy.Noun(Rows.Count, "medicine")} {Copy.Is(MissingPriceCount)} missing "
                + "prices. Fill them in, or use \u201cSave without prices\u201d.");

            return Page();
        }

        var request = new BulkImportRequest(Rows
            .Select(row => new BulkImportItemRequest(
                row.CatalogMedicineId,
                row.IsAntibiotic,
                row.BaseUnitName ?? "piece",
                row.MidUnitName,
                row.LargeUnitName,
                row.BasePerMid,
                row.MidPerLarge,

                // "Save without prices" sends nulls rather than zeros. Zero is a real price,
                // and a batch of two hundred products priced at nothing would be worse than
                // one that is honestly unpriced.
                withoutPrices ? null : row.PricePerBase,
                withoutPrices ? null : row.PricePerMid,
                withoutPrices ? null : row.PricePerLarge,
                row.ReorderLevel,
                Category: null,
                ShelfLocation: null))
            .ToList());

        var result = await _api.BulkImportProductsAsync(request, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        var outcome = result.Value!;

        if (!outcome.Succeeded)
        {
            // Nothing was created. Attach each failure to its row so the grid can show it in
            // place, and say plainly that nothing was saved — otherwise somebody sees errors
            // and assumes the rest went through.
            RowErrors = outcome.Failures
                .GroupBy(failure => failure.CatalogMedicineId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<string>)group
                        .SelectMany(failure => failure.Messages)
                        .ToList());

            FailureSummary = outcome.Failures
                .Select(failure => (
                    Name: failure.BrandName
                        ?? $"catalogue entry {failure.CatalogMedicineId}",
                    Reason: string.Join(" ", failure.Messages)))
                .ToList();

            ShowError(
                $"Nothing was imported — not even the {outcome.Items.Count - outcome.Failed} "
                + $"rows that were fine. {outcome.Failed} of {outcome.Items.Count} need "
                + "attention:");

            return Page();
        }

        // Imported: the selection has served its purpose and keeping it would leave the
        // footer bar offering to import products that now exist.
        HttpContext.Session.SetCatalogSelection(new CatalogSelection());

        // Written out rather than assembled from fragments: two whole sentences read better
        // than one with the pronoun and its capital letter spliced in.
        SuccessMessage = withoutPrices
            ? outcome.Created == 1
                ? "1 medicine added without prices. It will not be available for sale until "
                  + "its prices are set."
                : $"{outcome.Created} medicines added without prices. They will not be "
                  + "available for sale until prices are set."
            : $"{Copy.Count(outcome.Created, "medicine")} added.";

        return RedirectToPage("/Medicines/Index",
            withoutPrices ? new { status = ProductStatusFilter.SetupIncomplete } : null);
    }

    /// <summary>
    /// Pushes the shared preset onto every row that has not been individually overridden.
    ///
    /// <para>Rows that opted out keep what they were given, which is the whole point of the
    /// per-row override: a pharmacy importing forty tablets and two syrups sets the preset
    /// once and corrects two rows.</para>
    /// </summary>
    private void ApplyPresetToRows()
    {
        var (baseUnit, mid, large, basePerMid, midPerLarge) = UnitPresets.Shape(SharedPreset);

        foreach (var row in Rows.Where(row => row.UsesSharedUnits))
        {
            row.BaseUnitName = baseUnit;
            row.MidUnitName = mid;
            row.LargeUnitName = large;
            row.BasePerMid = basePerMid;
            row.MidPerLarge = midPerLarge;

            // A price for a level the row no longer has would be sent and rejected, so it is
            // cleared here — the same reason the single-product form clears on untick.
            if (mid is null)
            {
                row.PricePerMid = null;
            }

            if (large is null)
            {
                row.PricePerLarge = null;
            }
        }
    }

    private async Task<IActionResult?> LoadEntriesAsync(
        IReadOnlyList<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return RedirectToPage("/Medicines/Import");
        }

        var entries = new Dictionary<int, CatalogMedicineSearchItem>();

        // One call per id, because the catalogue lookup the client exposes is per entry. Only
        // this screen does it, at most two hundred times, once per visit — the import itself
        // batches properly server-side.
        foreach (var id in ids)
        {
            var entry = await _api.GetCatalogMedicineAsync(id, ct);

            if (entry.IsSuccess && entry.Value is not null)
            {
                entries[id] = entry.Value;
            }
        }

        Entries = entries;

        // Drop any row whose catalogue entry has vanished, rather than rendering a blank row
        // that cannot be saved.
        Rows = Rows.Where(row => entries.ContainsKey(row.CatalogMedicineId)).ToList();

        return null;
    }
}

/// <summary>One row of the bulk setup grid.</summary>
public sealed class BulkSetupRowInput
{
    public int CatalogMedicineId { get; set; }

    public bool IsAntibiotic { get; set; }

    /// <summary>
    /// False once the row has been customised, which exempts it from the shared preset.
    /// </summary>
    public bool UsesSharedUnits { get; set; } = true;

    public string? BaseUnitName { get; set; }

    public string? MidUnitName { get; set; }

    public string? LargeUnitName { get; set; }

    public int? BasePerMid { get; set; }

    public int? MidPerLarge { get; set; }

    public decimal? PricePerBase { get; set; }

    public decimal? PricePerMid { get; set; }

    public decimal? PricePerLarge { get; set; }

    /// <summary>
    /// Where the low-stock alert fires for this medicine. <b>No initialiser since Module 10</b> -
    /// the grid fills every row from the pharmacy's <c>default_reorder_level</c> setting, and the
    /// person editing the grid can change any of them.
    /// </summary>
    public int ReorderLevel { get; set; }

    /// <summary>Whether every level this row defines has a price.</summary>
    public bool HasAllPrices =>
        PricePerBase is not null
        && (MidUnitName is null || PricePerMid is not null)
        && (LargeUnitName is null || PricePerLarge is not null);
}
