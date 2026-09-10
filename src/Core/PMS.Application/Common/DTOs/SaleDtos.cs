using PMS.Domain.Enums;

namespace PMS.Application.Common.DTOs;

// ── The billing screen's product search ──────────────────────────────────────────────────

/// <summary>
/// Why a product cannot be put in the cart, or that it can.
///
/// <para><b>The billing screen shows unsellable products with the reason rather than hiding
/// them.</b> A cashier who types "napa" and sees nothing concludes the pharmacy does not stock
/// it and tells the customer so. A cashier who sees it greyed with "needs prices set" fetches
/// somebody who can fix it in fifteen seconds. Silence is the one outcome that wastes a sale
/// and teaches nobody anything.</para>
/// </summary>
public enum SellableStatus
{
    /// <summary>In stock, priced, and the caller's role may sell it.</summary>
    Sellable = 0,

    /// <summary>Imported in bulk and never priced. Admin or Pharmacist can complete setup.</summary>
    SetupIncomplete = 1,

    /// <summary>No batches with stock at all.</summary>
    OutOfStock = 2,

    /// <summary>Stock exists but every batch of it is past its expiry date.</summary>
    AllStockExpired = 3,

    /// <summary>An antibiotic, and the caller is an Employee.</summary>
    RequiresPharmacist = 4,
}

/// <summary>
/// One row in the billing screen's type-ahead: enough to price a cart line without a second
/// request, plus the reason it cannot be sold when it cannot.
/// </summary>
/// <param name="AvailableInBaseUnits">
/// Sellable stock across every batch — expired and inactive batches excluded, so this is the
/// number the quantity box is validated against.
/// </param>
/// <param name="ExpiredInBaseUnits">
/// Stock that exists but has expired. Only used to tell "we have none" apart from "we have some
/// and none of it can be sold", which are different conversations with a customer.
/// </param>
public sealed record SellableProductDto(
    Guid Id,
    string BrandName,
    string? GenericName,
    string? Strength,
    string? DosageForm,
    ProductType ProductType,
    bool IsAntibiotic,
    string BaseUnitName,
    string? MidUnitName,
    string? LargeUnitName,
    int? BasePerMid,
    int? BaseUnitsPerLarge,
    decimal? PricePerBase,
    decimal? PricePerMid,
    decimal? PricePerLarge,
    int AvailableInBaseUnits,
    int ExpiredInBaseUnits,
    string FormattedAvailable,
    SellableStatus Status,
    string? BlockedReason)
{
    public bool CanSell => Status == SellableStatus.Sellable;
}

// ── Completing a sale ────────────────────────────────────────────────────────────────────

/// <summary>One line of the cart as the client sent it.</summary>
/// <param name="Quantity">
/// In <paramref name="UnitLevel"/>, not base units. Decimal because a UI may offer "1.5 boxes";
/// the conversion refuses anything that is not a whole number of base units.
/// </param>
public sealed record CartItemRequest(Guid ProductId, decimal Quantity, UnitLevel UnitLevel);

/// <summary>The prescription an antibiotic sale must carry.</summary>
public sealed record PrescriptionRequest(
    string? PatientName,
    string? PatientPhone,
    string? DoctorName,
    string? PrescriptionNumber,
    DateOnly? PrescriptionDate,
    bool PrescriptionVerified);

/// <summary>What a completed sale reports back: enough to redirect to the invoice.</summary>
public sealed record SaleCompletedDto(
    Guid Id,
    string InvoiceNumber,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal NetTotal,
    decimal CashReceived,
    decimal ChangeGiven,
    int LineCount);

// ── Reading sales back ───────────────────────────────────────────────────────────────────

/// <summary>A row on the sales list.</summary>
/// <param name="ItemCount">
/// Distinct cart items as the customer would count them, not sale lines — a FEFO split is not
/// two things bought.
/// </param>
public sealed record SaleListItemDto(
    Guid Id,
    string InvoiceNumber,
    DateTime SaleDate,
    Guid CashierUserId,
    string? CashierName,
    int ItemCount,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal NetTotal,
    SaleStatus Status,
    bool HasReturns);

/// <summary>
/// One stored sale line: per batch, which is the granularity returns and profit need.
/// </summary>
public sealed record SaleLineDto(
    Guid Id,
    Guid ProductId,
    string BrandName,
    string? Strength,
    Guid BatchId,
    string BatchNumber,
    DateOnly? BatchExpiryDate,
    int QuantityInBaseUnits,
    UnitLevel UnitSold,
    string UnitSoldName,
    string BaseUnitName,
    decimal UnitSalePrice,
    decimal LineTotal,
    decimal DiscountShare,
    decimal NetLineTotal,
    int ReturnedInBaseUnits,
    decimal RefundedAmount)
{
    public int ReturnableInBaseUnits => QuantityInBaseUnits - ReturnedInBaseUnits;

    public bool IsFullyReturned => ReturnableInBaseUnits <= 0;
}

/// <summary>
/// One line as the <em>customer</em> sees it: the FEFO split merged back together.
///
/// <para>Merged on product and unit level. The sale price is copied from the product rather
/// than the batch, so every line of a split carries the same <c>UnitSalePrice</c> and the merge
/// is arithmetically clean — nothing is averaged and no cash figure is recomputed, the totals
/// are summed.</para>
/// </summary>
/// <param name="Quantity">In <paramref name="UnitSold"/> — "2" for two strips.</param>
/// <param name="BatchCount">
/// How many batches this row came out of. One on almost every row; shown nowhere on the
/// customer's copy, and useful on the staff-facing detail panel.
/// </param>
public sealed record InvoiceLineDto(
    Guid ProductId,
    string BrandName,
    string? Strength,
    string? DosageForm,
    bool IsAntibiotic,
    decimal Quantity,
    UnitLevel UnitSold,
    string UnitSoldName,
    int QuantityInBaseUnits,
    decimal UnitSalePrice,
    decimal LineTotal,
    decimal DiscountShare,
    decimal NetLineTotal,
    int ReturnedInBaseUnits,
    int BatchCount);

/// <summary>A sale in full, both ways round: grouped for the invoice, per batch for staff.</summary>
public sealed record SaleDetailDto(
    Guid Id,
    string InvoiceNumber,
    DateTime SaleDate,
    Guid CashierUserId,
    string? CashierName,
    decimal Subtotal,
    DiscountType? DiscountType,
    decimal? DiscountValue,
    decimal DiscountAmount,
    decimal NetTotal,
    decimal CashReceived,
    decimal ChangeGiven,
    SaleStatus Status,
    string? CancelledReason,
    string? CancelledByName,
    DateTime? CancelledAt,
    string? CustomerName,
    string? CustomerPhone,
    PrescriptionDto? Prescription,
    IReadOnlyList<InvoiceLineDto> InvoiceLines,
    IReadOnlyList<SaleLineDto> Lines,
    decimal TotalRefunded)
{
    /// <summary>"Discount (5%)" or "Discount (৳62.00)" — how the cashier expressed it.</summary>
    public string DiscountLabel => DiscountType switch
    {
        Domain.Enums.DiscountType.Percent => $"Discount ({DiscountValue:0.##}%)",
        Domain.Enums.DiscountType.Flat => "Discount",
        _ => "Discount",
    };
}

/// <summary>The prescription block, present only on a sale that contained an antibiotic.</summary>
public sealed record PrescriptionDto(
    string? PatientName,
    string? PatientPhone,
    string? DoctorName,
    string? PrescriptionNumber,
    DateOnly? PrescriptionDate,
    bool PrescriptionVerified);

/// <summary>
/// A line on the return screen, with what is left to return and what that would refund.
/// </summary>
/// <param name="RefundIfAllReturned">
/// What returning everything still returnable on this line pays back. Computed server-side and
/// shown before confirming, because the discount-adjusted figure is lower than the sticker
/// price and a cashier who is surprised by that at the moment of handing over cash will
/// hesitate in front of the customer.
/// </param>
public sealed record ReturnableLineDto(
    Guid SaleLineId,
    Guid ProductId,
    string BrandName,
    string? Strength,
    string BatchNumber,
    int QuantitySoldInBaseUnits,
    int ReturnedInBaseUnits,
    int ReturnableInBaseUnits,
    UnitLevel UnitSold,
    string BaseUnitName,
    string? MidUnitName,
    string? LargeUnitName,
    int? BasePerMid,
    int? BaseUnitsPerLarge,
    decimal UnitSalePrice,
    decimal NetLineTotal,
    decimal RefundedAmount,
    decimal RefundIfAllReturned,
    string FormattedSold,
    string FormattedReturnable)
{
    public bool IsFullyReturned => ReturnableInBaseUnits <= 0;
}

/// <summary>The lines of one sale that can still be returned, with the sale for context.</summary>
public sealed record ReturnableSaleDto(
    Guid SaleId,
    string InvoiceNumber,
    DateTime SaleDate,
    SaleStatus Status,
    decimal NetTotal,
    decimal TotalRefunded,
    IReadOnlyList<ReturnableLineDto> Lines);

/// <summary>What a completed return reports back.</summary>
public sealed record SalesReturnedDto(
    Guid Id,
    Guid SaleLineId,
    string InvoiceNumber,
    string BrandName,
    int QuantityReturnedInBaseUnits,
    string FormattedQuantity,
    decimal RefundAmount,
    string BatchNumber,
    int BatchQuantityAfter);

/// <summary>
/// What the caller is allowed to do at the till, for the billing screen to render its own
/// helper text and pre-validate.
///
/// <para><b>Why this is an endpoint rather than a constant in the web app.</b> PMS.Web
/// references no other project — it is purely an API client — so a cap shown on screen would
/// have to be a second copy of the number, and the two would drift the first time somebody
/// changed one. The screen asks. The answer still is not trusted: the same
/// <c>BillingPolicy</c> that produced it refuses the sale server-side.</para>
/// </summary>
/// <param name="MaxDiscountPercent">Null means no limit (an Admin).</param>
public sealed record BillingLimitsDto(
    decimal? MaxDiscountPercent,
    string CapDescription,
    bool MaySellAntibiotics,
    bool MayReturn,
    bool MayCancel);

// ── Filters ──────────────────────────────────────────────────────────────────────────────

/// <summary>Status filter on the sales list.</summary>
public enum SaleStatusFilter
{
    All = 0,
    Completed = 1,
    Cancelled = 2,
}
