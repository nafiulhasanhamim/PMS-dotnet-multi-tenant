using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.ViewModels;

namespace PMS.Web.Pages.Stock;

/// <summary>
/// Records one delivery.
///
/// <para><b>Admin or Pharmacist.</b> A pharmacist is the person who takes a delivery in, so a
/// module they could only read would be unusable.</para>
///
/// <para>The page reloads when the product changes, and that is deliberate rather than lazy:
/// the unit dropdowns, whether an expiry date is required, and the panel showing what is
/// already in stock all come from the server for that specific product. Rebuilding them in
/// JavaScript would be a second implementation of rules the server already owns — including
/// the two-level packing rule, which is the single most error-prone piece of arithmetic in
/// this system.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantWriter)]
public class AddModel : PmsPageModel
{
    /// <summary>
    /// How many products to offer in the picker before asking the person to narrow it.
    ///
    /// <para>A pharmacy can hold more than this. Rather than silently truncate — which would
    /// leave somebody scrolling for a product that is simply not in the list — the page says
    /// the list is partial and the filter box searches the server.</para>
    /// </summary>
    private const int PickerLimit = 100;

    private readonly PmsApiClient _api;

    public AddModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty]
    public BatchFormInput Input { get; set; } = new();

    /// <summary>Narrows the product picker. Searched server-side, so it scales.</summary>
    [BindProperty(SupportsGet = true, Name = "pq")]
    public string? ProductQuery { get; set; }

    /// <summary>The products offered in the picker.</summary>
    public IReadOnlyList<ProductListItem> Products { get; private set; } = [];

    /// <summary>True when the picker is showing only part of the catalogue.</summary>
    public bool ProductsTruncated { get; private set; }

    /// <summary>The chosen product in full — the source of every unit dropdown on this form.</summary>
    public PMS.Web.Api.ProductModel? Product { get; private set; }

    /// <summary>What is already in stock for the chosen product, so nobody enters it twice.</summary>
    public ProductStockModel? ExistingStock { get; private set; }

    /// <summary>Locked when the page was reached from a product, so the context cannot change.</summary>
    public bool ProductLocked { get; private set; }

    public IReadOnlyList<(UnitLevel Level, string Name)> UnitOptions =>
        Product is null ? [] : StockPresentation.UnitOptions(Product);

    public bool ExpiryRequired => Product?.ProductType == Api.ProductType.Medicine;

    public async Task<IActionResult> OnGetAsync(
        Guid? productId, bool locked, CancellationToken ct)
    {
        if (productId is { } id && id != Guid.Empty)
        {
            Input.ProductId = id;
            ProductLocked = locked;
        }

        var redirect = await LoadContextAsync(ct);

        return redirect ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var redirect = await LoadContextAsync(ct);
        if (redirect is not null)
        {
            return redirect;
        }

        if (Product is null)
        {
            ModelState.AddModelError(
                "Input.ProductId", "Choose the product this stock is for.");
            return Page();
        }

        ValidateLocally();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _api.CreateBatchAsync(
            new CreateBatchRequest(
                Input.ProductId,
                Input.BatchNumber?.Trim() ?? string.Empty,
                Input.ExpiryDate,
                Input.ManufactureDate,
                Input.Quantity ?? 0,
                Input.QuantityUnit,
                Input.PurchasePrice ?? 0,
                Input.PurchasePriceUnit,
                // Module 4's retrofit. The id is the record; the free-text name is kept
                // alongside it because that is what was written on the delivery note, and
                // because every batch entered before this module has only the text.
                Input.SupplierId,
                Input.SupplierNameText,
                Input.Notes),
            ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        var created = result.Value!;

        // Names the quantity in base units, because that is the number the pharmacy now holds
        // and it is not the number that was typed. "2 cartons" saved as 48 bottles is exactly
        // the conversion somebody wants confirmed.
        SuccessMessage =
            $"Batch added. {created.Batch.FormattedQuantity} of "
            + $"{created.Batch.ProductBrandName} recorded.";

        if (created.SellsAtALoss && created.Warning is not null)
        {
            // The form warns before posting, from the price on screen. This catches the case
            // where the product was repriced in between, and it is worth saying out loud
            // rather than leaving the batch quietly loss-making.
            SuccessMessage += $" Note: {created.Warning}";
        }

        return RedirectToPage("/Stock/Detail", new { productId = Input.ProductId });
    }

    /// <summary>
    /// Loads the product picker and, when a product is chosen, everything that depends on it.
    /// Runs on both GET and POST so a redisplayed form is never missing its unit dropdowns.
    /// </summary>
    /// <summary>Active suppliers for the dropdown. Module 4's retrofit.</summary>
    public IReadOnlyList<SupplierOption> Suppliers { get; private set; } = [];

    private async Task<IActionResult?> LoadContextAsync(CancellationToken ct)
    {
        // Module 4's retrofit: the supplier field was free text until Supplier existed. Active
        // suppliers only - a deactivated one is somebody the pharmacy has stopped buying from.
        // A failure here is not fatal: the dropdown renders empty, the field stays optional, and
        // the batch still saves without a supplier.
        var suppliers = await _api.GetSupplierOptionsAsync(ct);

        if (suppliers.IsSuccess)
        {
            Suppliers = suppliers.Value ?? [];
        }

        var medicines = await _api.GetProductsAsync(
            ProductListType.Medicine, ProductQuery, ProductStatusFilter.Active,
            page: 1, pageSize: PickerLimit, ct: ct);

        if (!medicines.IsSuccess)
        {
            return await HandleFailureAsync(medicines);
        }

        var others = await _api.GetProductsAsync(
            ProductListType.Other, ProductQuery, ProductStatusFilter.Active,
            page: 1, pageSize: PickerLimit, ct: ct);

        if (!others.IsSuccess)
        {
            return await HandleFailureAsync(others);
        }

        var medicinePage = medicines.Value ?? ApiPage<ProductListItem>.Empty;
        var otherPage = others.Value ?? ApiPage<ProductListItem>.Empty;

        ProductsTruncated = medicinePage.Total > medicinePage.Data.Count
            || otherPage.Total > otherPage.Data.Count;

        Products = medicinePage.Data
            .Concat(otherPage.Data)
            .OrderBy(p => p.BrandName)
            .ToList();

        if (Input.ProductId == Guid.Empty)
        {
            return null;
        }

        var product = await _api.GetProductAsync(Input.ProductId, ct);

        if (!product.IsSuccess)
        {
            if (product.Problem?.Status == StatusCodes.Status404NotFound)
            {
                ModelState.AddModelError(
                    "Input.ProductId", "That product is no longer available.");
                Input.ProductId = Guid.Empty;
                return null;
            }

            return await HandleFailureAsync(product);
        }

        Product = product.Value;

        // Loaded so the form can show what is already there. Somebody entering a delivery of
        // Napa should be able to see that B-100 already exists before they type it again.
        var stock = await _api.GetProductStockAsync(Input.ProductId, ct: ct);

        if (stock.IsSuccess)
        {
            ExistingStock = stock.Value;
        }

        // A product whose picker entry was filtered out still has to appear in the select, or
        // the form would redisplay having silently forgotten what it was for.
        if (Product is not null && Products.All(p => p.Id != Product.Id))
        {
            Products = Products
                .Append(new ProductListItem(
                    Product.Id, Product.ProductType, Product.BrandName, Product.GenericName,
                    Product.Company, Product.Strength, Product.DosageForm, Product.Category,
                    Product.IsAntibiotic, Product.IsActive, Product.BaseUnitName,
                    Product.PricePerBase, Product.ImportedFromCatalog,
                    Product.IsSetupComplete))
                .OrderBy(p => p.BrandName)
                .ToList();
        }

        return null;
    }

    /// <summary>
    /// Client-side checks mirroring the server, to save a round trip on an obvious mistake.
    /// The API stays authoritative: whatever it rejects lands on the same inputs.
    /// </summary>
    private void ValidateLocally()
    {
        if (string.IsNullOrWhiteSpace(Input.BatchNumber))
        {
            ModelState.AddModelError(
                "Input.BatchNumber", "Enter the batch number from the pack.");
        }

        if (Input.Quantity is null or <= 0)
        {
            ModelState.AddModelError("Input.Quantity", "Enter how much arrived.");
        }

        if (Input.PurchasePrice is null or < 0)
        {
            ModelState.AddModelError("Input.PurchasePrice", "Enter what it cost.");
        }

        // The conditional rule, mirrored. The server enforces it from the product type too,
        // which is what actually decides it — this only saves the round trip.
        if (ExpiryRequired && Input.ExpiryDate is null)
        {
            ModelState.AddModelError(
                "Input.ExpiryDate",
                $"{Product?.BrandName} is a medicine, so its expiry date is required.");
        }

        if (Input.ExpiryDate is { } expiry && expiry < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            ModelState.AddModelError(
                "Input.ExpiryDate", "That expiry date has already passed. Check the pack.");
        }

        if (Input.ManufactureDate is { } made && Input.ExpiryDate is { } expires
            && made >= expires)
        {
            ModelState.AddModelError(
                "Input.ManufactureDate", "The manufacture date has to be before the expiry date.");
        }
    }
}

/// <summary>
/// The add-stock form.
///
/// <para>Quantity and price carry their own unit level, because what somebody types is "20
/// strips at 8 taka a strip" and the conversion to 200 pieces at 0.80 belongs on the server.
/// </para>
/// </summary>
public sealed class BatchFormInput
{
    public Guid ProductId { get; set; }

    public string? BatchNumber { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public DateOnly? ManufactureDate { get; set; }

    public decimal? Quantity { get; set; }

    public UnitLevel QuantityUnit { get; set; } = UnitLevel.Base;

    public decimal? PurchasePrice { get; set; }

    public UnitLevel PurchasePriceUnit { get; set; } = UnitLevel.Base;

    /// <summary>
    /// Chosen from the dropdown. Still optional: opening inventory, a free sample and a
    /// correction all arrive without a supplier, and that is what this screen is for.
    /// </summary>
    public Guid? SupplierId { get; set; }

    /// <summary>
    /// Kept, and not replaced by the dropdown. Batches entered before Module 4 have only this,
    /// and they still display it — see the docs on the historical-data gap.
    /// </summary>
    public string? SupplierNameText { get; set; }

    public string? Notes { get; set; }
}
