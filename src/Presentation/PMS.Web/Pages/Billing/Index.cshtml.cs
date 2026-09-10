using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Billing;

/// <summary>
/// The till. The busiest screen in the product, and the only one a person uses for hours.
///
/// <para><b>Keyboard first.</b> The search box is focused on load, Enter adds the highlighted
/// result, and every subsequent field is reachable by Tab in the order a sale actually happens.
/// A cashier with a queue should not be reaching for a mouse, and the layout is arranged around
/// that rather than around what looks tidy.</para>
///
/// <para><b>What the client computes and what it does not.</b> The bill summary updates live in
/// the browser, because a round trip per keystroke would make the screen feel broken. None of
/// those figures are sent: the form posts product ids, quantities and unit levels, and the
/// server prices the sale from the products and the discount from its own arithmetic. The
/// numbers on screen are a preview of a calculation performed somewhere the customer cannot
/// reach.</para>
///
/// <para><b>Why the cart survives a rejection.</b> Each row also posts the product's name, unit
/// name and price as hidden fields, used only to redraw the row if the server refuses the sale.
/// They are never read as money — losing a cart of fifteen items because one batch sold out
/// mid-sale would be a far worse failure than the one being reported.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantUser)]
public class IndexModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public IndexModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty]
    public BillingInput Input { get; set; } = new();

    /// <summary>
    /// The caller's discount cap and what they may dispense, read from the API.
    ///
    /// <para>Not hard-coded here. This app references no other project, so a cap written into
    /// it would be a second copy of a number the API enforces, and the copy would go stale the
    /// first time somebody changed the real one.</para>
    /// </summary>
    public BillingLimits Limits { get; private set; } = BillingLimits.None;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var limits = await _api.GetBillingLimitsAsync(ct);

        if (!limits.IsSuccess)
        {
            return await HandleFailureAsync(limits) ?? Page();
        }

        Limits = limits.Value ?? BillingLimits.None;

        return Page();
    }

    /// <summary>
    /// The type-ahead, proxied through this app so the browser never holds an API token.
    ///
    /// <para>Returns the API's own rows unchanged, including the unsellable ones and their
    /// reasons — the screen greys those and shows why rather than dropping them, which is the
    /// difference between a cashier fetching a pharmacist and a cashier telling a customer the
    /// pharmacy does not stock something that is on the shelf behind them.</para>
    /// </summary>
    public async Task<IActionResult> OnGetSearchAsync(string? q, CancellationToken ct)
    {
        var result = await _api.SearchSellableAsync(q, limit: 0, ct);

        if (!result.IsSuccess)
        {
            // A failed search must not take the page down: the cashier still has a cart. The
            // status tells the script to show an inline message instead of an empty dropdown,
            // which would read as "no such product".
            return new JsonResult(new { error = result.Problem?.Message ?? "Search failed." })
            {
                StatusCode = StatusCodes.Status502BadGateway,
            };
        }

        return new JsonResult(result.Value ?? []);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var limits = await _api.GetBillingLimitsAsync(ct);
        Limits = limits.Value ?? BillingLimits.None;

        var rows = Input.Items?.Where(row => row.ProductId != Guid.Empty).ToList() ?? [];

        if (rows.Count == 0)
        {
            ShowError("Add at least one item before completing the sale.");
            return Page();
        }

        var payload = new CompleteSalePayload(
            rows.Select(row => new CartItemPayload(row.ProductId, row.Quantity, row.UnitLevel))
                .ToList(),
            Input.DiscountValue is > 0 ? Input.DiscountType : null,
            Input.DiscountValue is > 0 ? Input.DiscountValue : null,
            Input.CashReceived,
            Input.CustomerName,
            Input.CustomerPhone,

            // Sent only when the cart actually holds an antibiotic. Posting a half-filled
            // prescription for a sale that does not need one would turn the server's
            // all-or-nothing check into a puzzling rejection.
            rows.Any(row => row.IsAntibiotic)
                ? new PrescriptionPayload(
                    Input.PatientName,
                    Input.PatientPhone,
                    Input.DoctorName,
                    Input.PrescriptionNumber,
                    Input.PrescriptionDate,
                    Input.PrescriptionVerified)
                : null);

        var result = await _api.CompleteSaleAsync(payload, ct);

        if (!result.IsSuccess)
        {
            // Re-render with the cart intact. Field errors from the API land on their inputs
            // through ApplyProblem; anything else becomes the banner.
            return await HandleFailureAsync(result) ?? Page();
        }

        var sale = result.Value!;

        SuccessMessage =
            $"Invoice {sale.InvoiceNumber} completed. "
            + $"Net {sale.NetTotal:N2}, change {sale.ChangeGiven:N2}.";

        return RedirectToPage("/Sales/Detail", new { id = sale.Id });
    }

    /// <summary>
    /// The API names discount fields <c>DiscountValue</c> and prescription fields
    /// <c>PatientName</c>; the form binds them under <c>Input</c>. The base class maps the
    /// common case, and cart-item errors are the exception: the API reports them against
    /// <c>Items</c>, which is a collection with no single input to attach to, so those land in
    /// the banner where the cashier can read which product it was about.
    /// </summary>
    protected override string ModelStateKeyFor(string apiField) =>
        apiField == "Items" ? string.Empty : $"Input.{apiField}";
}

/// <summary>The whole bill as the form posts it.</summary>
public sealed class BillingInput
{
    public List<CartRowInput>? Items { get; set; }

    public DiscountType? DiscountType { get; set; }

    public decimal? DiscountValue { get; set; }

    public decimal CashReceived { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string? PatientName { get; set; }

    public string? PatientPhone { get; set; }

    public string? DoctorName { get; set; }

    public string? PrescriptionNumber { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? PrescriptionDate { get; set; }

    public bool PrescriptionVerified { get; set; }
}

/// <summary>
/// One cart row.
///
/// <para>Only the first three fields reach the API. The rest exist so a rejected sale can be
/// redrawn without a round trip per row, and are never read as money — see the page
/// remarks.</para>
/// </summary>
public sealed class CartRowInput
{
    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    public UnitLevel UnitLevel { get; set; }

    // ── display only, below this line ────────────────────────────────────────────────────

    public string? BrandName { get; set; }

    public string? Strength { get; set; }

    public string? UnitName { get; set; }

    public decimal UnitPrice { get; set; }

    public int AvailableInBaseUnits { get; set; }

    public string? FormattedAvailable { get; set; }

    public bool IsAntibiotic { get; set; }

    /// <summary>The product's unit configuration, so the row's dropdown can be rebuilt.</summary>
    public string? UnitOptionsJson { get; set; }
}
