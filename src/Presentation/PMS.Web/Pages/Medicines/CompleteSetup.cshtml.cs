using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.ViewModels;

namespace PMS.Web.Pages.Medicines;

/// <summary>
/// Fills in the prices of products that were imported without them.
///
/// <para>The same grid as the bulk setup screen, at a different moment: there the rows come
/// from a catalogue selection, here from the pharmacy's own unpriced products. Reusing the
/// partial keeps the copy-down affordance and the keyboard behaviour identical, which matters
/// because this is the screen somebody works through fifty rows of.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantWriter)]
public class CompleteSetupModel : PmsPageModel
{
    /// <summary>
    /// One page of incomplete products. Matches the API's per-request cap, so a pharmacy with
    /// more than this completes them in more than one save rather than being refused.
    /// </summary>
    private const int PageSize = 100;

    private readonly PmsApiClient _api;

    public CompleteSetupModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty]
    public List<CompleteSetupRowInput> Rows { get; set; } = [];

    /// <summary>The products behind the rows, for the read-only columns and unit names.</summary>
    public IReadOnlyDictionary<Guid, ProductListItem> Products { get; private set; }
        = new Dictionary<Guid, ProductListItem>();

    /// <summary>Unit configuration per product, needed to know which price columns to show.</summary>
    public IReadOnlyDictionary<Guid, ProductModel> Details { get; private set; }
        = new Dictionary<Guid, ProductModel>();

    public IReadOnlyDictionary<Guid, IReadOnlyList<string>> RowErrors { get; private set; }
        = new Dictionary<Guid, IReadOnlyList<string>>();

    public int TotalIncomplete { get; private set; }

    public int MissingPriceCount => Rows.Count(row => !row.HasAllPrices(Details));

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var redirect = await LoadAsync(ct);
        if (redirect is not null)
        {
            return redirect;
        }

        Rows = Details.Values
            .OrderBy(product => product.BrandName)
            .Select(product => new CompleteSetupRowInput { ProductId = product.Id })
            .ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var redirect = await LoadAsync(ct);
        if (redirect is not null)
        {
            return redirect;
        }

        // Only rows still present in the reload — a product completed in another tab is no
        // longer here, and posting a price for it would be answered with "not found".
        Rows = Rows.Where(row => Details.ContainsKey(row.ProductId)).ToList();

        if (Rows.Count == 0)
        {
            SuccessMessage = "Every product already has its prices.";
            return RedirectToPage("/Medicines/Index");
        }

        if (MissingPriceCount > 0)
        {
            ShowError(
                $"{MissingPriceCount} of {Rows.Count} "
                + $"{Copy.Noun(Rows.Count, "product")} {Copy.Is(MissingPriceCount)} still "
                + "missing prices. Every price has to be filled in before saving.");

            return Page();
        }

        var request = new SetPricesRequest(Rows
            .Select(row => new ProductPriceUpdateRequest(
                row.ProductId,
                row.PricePerBase ?? 0m,
                row.PricePerMid,
                row.PricePerLarge))
            .ToList());

        var result = await _api.SetProductPricesAsync(request, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        var outcome = result.Value!;

        if (!outcome.Succeeded)
        {
            RowErrors = outcome.Failures
                .Where(failure => failure.Row < Rows.Count)
                .GroupBy(failure => Rows[failure.Row].ProductId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<string>)group
                        .SelectMany(failure => failure.Messages)
                        .ToList());

            ShowError(
                $"Nothing was saved. {outcome.Failed} rows have a problem — they are marked "
                + "below.");

            return Page();
        }

        SuccessMessage =
            $"Prices set for {Copy.Count(outcome.Created, "product")}. "
            + $"{(outcome.Created == 1 ? "It is" : "They are")} ready to sell.";

        return RedirectToPage("/Medicines/Index");
    }

    private async Task<IActionResult?> LoadAsync(CancellationToken ct)
    {
        var page = await _api.GetProductsAsync(
            ProductListType.Medicine,
            search: null,
            status: ProductStatusFilter.SetupIncomplete,
            page: 1,
            pageSize: PageSize,
            ct: ct);

        if (!page.IsSuccess)
        {
            return await HandleFailureAsync(page);
        }

        var list = page.Value ?? ApiPage<ProductListItem>.Empty;

        TotalIncomplete = list.Total;
        Products = list.Data.ToDictionary(product => product.Id);

        if (Products.Count == 0)
        {
            // Nothing to complete. Landing on an empty grid reads as a broken page, so send
            // them to the list with an explanation.
            SuccessMessage = "Every medicine has its prices. Nothing needs completing.";
            return RedirectToPage("/Medicines/Index");
        }

        // The list row does not carry the unit configuration, and the grid needs it to know
        // which price columns a product even has.
        var details = new Dictionary<Guid, ProductModel>();

        foreach (var id in Products.Keys)
        {
            var detail = await _api.GetProductAsync(id, ct);

            if (detail.IsSuccess && detail.Value is not null)
            {
                details[id] = detail.Value;
            }
        }

        Details = details;

        return null;
    }
}

public sealed class CompleteSetupRowInput
{
    public Guid ProductId { get; set; }

    public decimal? PricePerBase { get; set; }

    public decimal? PricePerMid { get; set; }

    public decimal? PricePerLarge { get; set; }

    /// <summary>
    /// Whether this row has every price its product needs. Asks the product rather than the
    /// row, because which levels exist is the product's business.
    /// </summary>
    public bool HasAllPrices(IReadOnlyDictionary<Guid, ProductModel> details)
    {
        if (!details.TryGetValue(ProductId, out var product))
        {
            return true;
        }

        return PricePerBase is not null
            && (product.MidUnitName is null || PricePerMid is not null)
            && (product.LargeUnitName is null || PricePerLarge is not null);
    }
}
