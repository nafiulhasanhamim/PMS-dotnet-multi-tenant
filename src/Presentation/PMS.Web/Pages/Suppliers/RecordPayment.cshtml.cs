using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Suppliers;

/// <summary>
/// Records money paid to a supplier. <b>Admin only.</b>
///
/// <para>Financial settlement is the one thing a Pharmacist cannot do — they can record what
/// arrived and what went back, both of which are stock events they are present for, but deciding
/// that money has left the till is the owner's. The policy here mirrors the API's.</para>
///
/// <para><b>Money can go either way.</b> When the supplier is holding a credit — goods returned
/// after a bill was paid, or a payment that overshot — this page also records the supplier handing
/// it back, or the pharmacy giving up on collecting it. Those options only appear when there IS a
/// credit, because offering "record a refund" to somebody who is owed nothing is an invitation to
/// record a payment in the wrong direction.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class RecordPaymentModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public RecordPaymentModel(PmsApiClient api) => _api = api;

    [BindProperty(SupportsGet = true, Name = "id")]
    public Guid SupplierId { get; set; }

    /// <summary>Pre-selects a bill when the person arrived from a purchase page.</summary>
    [BindProperty(SupportsGet = true, Name = "purchase")]
    public Guid? PreselectedPurchaseId { get; set; }

    [BindProperty]
    public PaymentInput Input { get; set; } = new();

    public SupplierDetail Supplier { get; private set; } = SupplierDetail.Empty;

    public IReadOnlyList<SupplierPurchaseRow> UnsettledPurchases { get; private set; } = [];

    /// <summary>
    /// Whether this supplier is holding a credit, and so whether the incoming options make sense.
    ///
    /// <para>Offering "record a refund" on an account that owes money would invite somebody to
    /// record a payment in the wrong direction, which is the one mistake here that silently
    /// doubles a debt.</para>
    /// </summary>
    public bool CanSettleCredit => Supplier.Balance.IsOverpaid;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var loaded = await LoadAsync(ct);

        if (loaded is not null)
        {
            return loaded;
        }

        Input.PurchaseId = PreselectedPurchaseId;
        Input.PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow);
        Input.PaymentMethod = "Cash";

        // Pre-fills the credit when there is one, because settling it in full is what somebody
        // came here to do nine times out of ten. Still editable: a supplier may hand back part.
        if (CanSettleCredit)
        {
            Input.Amount = Supplier.Balance.CreditAvailable;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var loaded = await LoadAsync(ct);

        if (loaded is not null)
        {
            return loaded;
        }

        // A refund or a write-off settles the ACCOUNT, never one bill — so any purchase the form
        // carried over from a previous selection is dropped rather than sent and rejected.
        if (Input.Direction != SupplierPaymentDirection.Payment)
        {
            Input.PurchaseId = null;
        }

        if (Input.Direction == SupplierPaymentDirection.WriteOff
            && string.IsNullOrWhiteSpace(Input.Notes))
        {
            ModelState.AddModelError(
                "Input.Notes", "Say why this credit is being written off.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _api.RecordPaymentAsync(
            SupplierId,
            new RecordPaymentPayload(
                Input.PurchaseId, Input.Amount, Input.PaymentDate,
                Input.PaymentMethod, Input.Notes, Input.Direction),
            ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        var recorded = result.Value!;

        var what = Input.Direction switch
        {
            SupplierPaymentDirection.Refund => "Refund",
            SupplierPaymentDirection.WriteOff => "Write-off",
            _ => "Payment",
        };

        // The overshoot warning travels through to the supplier page rather than blocking the
        // save: the money moved, and refusing to record it would be the wrong way round.
        SuccessMessage = recorded.ExceedsBalance
            ? $"{what} of {recorded.Amount:N2} recorded. {recorded.Warning}"
            : $"{what} of {recorded.Amount:N2} recorded. "
              + $"Outstanding is now {recorded.BalanceAfter.Outstanding:N2}.";

        return RedirectToPage("/Suppliers/Detail", new { id = SupplierId });
    }

    private async Task<IActionResult?> LoadAsync(CancellationToken ct)
    {
        var supplier = await _api.GetSupplierAsync(SupplierId, ct);

        if (!supplier.IsSuccess)
        {
            return await HandleFailureAsync(supplier) ?? NotFound();
        }

        Supplier = supplier.Value ?? SupplierDetail.Empty;

        var unsettled = await _api.GetUnsettledPurchasesAsync(SupplierId, ct);

        if (unsettled.IsSuccess)
        {
            UnsettledPurchases = unsettled.Value ?? [];
        }

        return null;
    }

    public sealed class PaymentInput
    {
        /// <summary>
        /// Which way the money is going. Defaults to a payment out, which is what this page was
        /// for before credits could be settled.
        /// </summary>
        public SupplierPaymentDirection Direction { get; set; }
            = SupplierPaymentDirection.Payment;

        /// <summary>Null means a general payment against the account.</summary>
        public Guid? PurchaseId { get; set; }

        [Range(0.01, 99999999, ErrorMessage = "Enter an amount greater than zero.")]
        public decimal Amount { get; set; }

        public DateOnly? PaymentDate { get; set; }

        [StringLength(40)]
        public string? PaymentMethod { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }
    }
}
