using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Purchases;

/// <summary>
/// Records a delivery: a supplier, a date, and one row per product that arrived.
///
/// <para><b>The rows are the form.</b> They are added and removed in the browser and posted as an
/// indexed collection, so the whole delivery is one request and one transaction — a failure on the
/// third row leaves nothing behind. Posting row by row would create batches that a later failure
/// could not take back.</para>
///
/// <para><b>The browser does no packing arithmetic.</b> Quantity and price go up in whatever unit
/// the person chose, with the level alongside, exactly as Add Stock sends them; the server converts
/// through Module 2's helpers. The live "= 200 pieces" helper beside each row is a convenience
/// computed from the product's own factors for display only — if it ever disagreed with the server,
/// the server would still be right, and nothing is stored from it.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantWriter)]
public class CreateModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public CreateModel(PmsApiClient api) => _api = api;

    /// <summary>Pre-fills the supplier when arriving from their detail page.</summary>
    [BindProperty(SupportsGet = true, Name = "supplier")]
    public Guid? PreselectedSupplierId { get; set; }

    [BindProperty]
    public PurchaseInput Input { get; set; } = new();

    public IReadOnlyList<SupplierOption> Suppliers { get; private set; } = [];

    /// <summary>
    /// The products the rows can choose from, with their unit configuration.
    ///
    /// <para>Sent to the browser as JSON so each row's two unit dropdowns can be rebuilt when a
    /// product is chosen — Napa offers piece/strip/box, the handwash bottle/carton, saline bag
    /// only. A single shared unit list would offer a strip of handwash.</para>
    /// </summary>
    public IReadOnlyList<PurchaseProductOption> Products { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var loaded = await LoadAsync(ct);

        if (loaded is not null)
        {
            return loaded;
        }

        Input.SupplierId = PreselectedSupplierId ?? Guid.Empty;
        Input.PurchaseDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // One empty row, so the form opens ready to type into rather than needing a click first.
        Input.Lines = [new LineInput()];

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var loaded = await LoadAsync(ct);

        if (loaded is not null)
        {
            return loaded;
        }

        // Rows the browser removed come back as empty slots in the indexed collection; they are
        // dropped rather than validated, or removing the second of three rows would fail the form.
        var lines = Input.Lines
            .Where(l => l.ProductId is not null && l.ProductId != Guid.Empty)
            .ToList();

        if (lines.Count == 0)
        {
            ModelState.AddModelError("Input.Lines", "Add at least one item to this purchase.");
        }

        if (Input.SupplierId == Guid.Empty)
        {
            ModelState.AddModelError(
                "Input.SupplierId", "Choose the supplier this delivery came from.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _api.CreatePurchaseAsync(
            new CreatePurchasePayload(
                Input.SupplierId,
                Input.PurchaseDate,
                Input.Notes,
                lines.Select(l => new CreatePurchaseLinePayload(
                        l.ProductId!.Value,
                        l.BatchNumber ?? string.Empty,
                        l.ExpiryDate,
                        l.ManufactureDate,
                        l.Quantity,
                        l.QuantityUnit,
                        l.PurchasePrice,
                        l.PurchasePriceUnit,
                        null))
                    .ToList()),
            ct);

        if (!result.IsSuccess)
        {
            // Every rule that can refuse a row - a duplicate batch number, a medicine with no
            // expiry, a quantity that does not divide into whole base units - belongs to Module
            // 3's batch command and comes back named by line. Restating any of them here would
            // be a second implementation that could disagree with Add Stock.
            return await HandleFailureAsync(result) ?? Page();
        }

        var created = result.Value!;

        SuccessMessage =
            $"Purchase {created.PurchaseNumber} recorded. "
            + $"{created.BatchesCreated} batch{(created.BatchesCreated == 1 ? "" : "es")} added.";

        if (created.Warnings.Count > 0)
        {
            // Non-blocking: bought above list price is a real situation, and the pharmacy may
            // simply be about to reprice. Worth saying once, not worth refusing over.
            TempData["PurchaseWarnings"] = string.Join(" ", created.Warnings);
        }

        return RedirectToPage("/Purchases/Detail", new { id = created.PurchaseId });
    }

    private async Task<IActionResult?> LoadAsync(CancellationToken ct)
    {
        var suppliers = await _api.GetSupplierOptionsAsync(ct);

        if (!suppliers.IsSuccess)
        {
            return await HandleFailureAsync(suppliers) ?? Page();
        }

        Suppliers = suppliers.Value ?? [];

        // Reuses the billing screen's product search, which already returns every ACTIVE product
        // with its full unit configuration - including ones that are out of stock or not yet
        // priced. That is exactly right here and would be wrong to filter: a product with no
        // stock is the most likely thing somebody is buying, and one whose setup is incomplete
        // still arrives in a delivery. The Status field it carries is ignored; it describes
        // whether a product can be SOLD, which is a different question.
        //
        // Fetched once and filtered in the browser, so a five-row form makes one request rather
        // than one per keystroke per row.
        var products = await _api.SearchSellableAsync(search: null, limit: 0, ct: ct);

        if (products.IsSuccess)
        {
            Products = (products.Value ?? [])
                .Select(p => new PurchaseProductOption(
                    p.Id, p.BrandName, p.GenericName, p.ProductType,
                    p.BaseUnitName, p.MidUnitName, p.LargeUnitName,
                    p.BasePerMid, p.BaseUnitsPerLarge, p.PricePerBase))
                .OrderBy(p => p.BrandName)
                .ToList();
        }

        return null;
    }

    public sealed class PurchaseInput
    {
        public Guid SupplierId { get; set; }

        public DateOnly? PurchaseDate { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public List<LineInput> Lines { get; set; } = [];
    }

    public sealed class LineInput
    {
        public Guid? ProductId { get; set; }

        [StringLength(100)]
        public string? BatchNumber { get; set; }

        public DateOnly? ExpiryDate { get; set; }

        public DateOnly? ManufactureDate { get; set; }

        public decimal Quantity { get; set; }

        public UnitLevel QuantityUnit { get; set; } = UnitLevel.Base;

        public decimal PurchasePrice { get; set; }

        public UnitLevel PurchasePriceUnit { get; set; } = UnitLevel.Base;
    }

    /// <param name="PricePerBase">
    /// The sale price, so a row can warn when the purchase price would exceed it. Null for a
    /// product whose setup is incomplete, in which case there is nothing to compare against —
    /// and an unpriced product cannot sell at a loss because it cannot sell at all.
    /// </param>
    public sealed record PurchaseProductOption(
        Guid Id,
        string BrandName,
        string? GenericName,
        ProductType ProductType,
        string BaseUnitName,
        string? MidUnitName,
        string? LargeUnitName,
        int? BasePerMid,

        /// <summary>
        /// Base units in one large unit, not mid units in one large. A carton is 24 bottles
        /// directly; a box is 100 pieces via 10 strips. The row's helper text multiplies by this.
        /// </summary>
        int? BaseUnitsPerLarge,
        decimal? PricePerBase);
}
