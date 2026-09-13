using PMS.Domain.Enums;

namespace PMS.Application.Common.DTOs;

// ── Purchases ────────────────────────────────────────────────────────────────────────────

/// <param name="GeneralPaymentApplied">
/// See <c>SupplierPurchaseRowDto</c>. Display only, and included in <paramref name="Due"/> so
/// this list agrees with the supplier page about whether a bill is settled.
/// </param>
public sealed record PurchaseListItemDto(
    Guid Id,
    string PurchaseNumber,
    Guid SupplierId,
    string SupplierName,
    DateOnly PurchaseDate,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal ReturnedAmount,
    decimal GeneralPaymentApplied,
    decimal CreditSettled,
    decimal Due,
    PurchasePaymentStatus Status,
    int LineCount);

/// <param name="QuantityInBaseUnits">
/// What was delivered, frozen at purchase time. <b>Not the batch's current quantity</b> — that
/// falls as stock sells, and a purchase total that moved with it would restate a supplier's bill
/// every time a customer bought something.
/// </param>
/// <param name="ReturnedInBaseUnits">
/// How much of this line has gone back. Shown as a "Returned: X" indicator beside the row rather
/// than deducted from the quantity, because the quantity is what the bill said.
/// </param>
/// <param name="BatchQuantityInBaseUnits">
/// What the batch holds now. Differs from the delivered quantity as soon as anything sells, and
/// it is the second cap on what can still be returned.
/// </param>
public sealed record PurchaseLineDto(
    Guid Id,
    Guid ProductId,
    string BrandName,
    string? GenericName,
    ProductType ProductType,
    Guid BatchId,
    string BatchNumber,
    DateOnly? ExpiryDate,
    int QuantityInBaseUnits,
    string FormattedQuantity,
    decimal PurchasePricePerBaseUnit,
    string BaseUnitName,
    decimal LineTotal,
    int ReturnedInBaseUnits,
    string? FormattedReturned,
    int BatchQuantityInBaseUnits)
{
    public bool HasReturns => ReturnedInBaseUnits > 0;
}

public sealed record PurchaseDetailDto(
    Guid Id,
    string PurchaseNumber,
    Guid SupplierId,
    string SupplierName,
    string SupplierPhone,
    DateOnly PurchaseDate,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal ReturnedAmount,
    decimal GeneralPaymentApplied,
    decimal CreditSettled,
    decimal Due,
    PurchasePaymentStatus Status,
    string? Notes,
    string? CreatedByName,
    DateTime CreatedOnUtc,
    IReadOnlyList<PurchaseLineDto> Lines,
    IReadOnlyList<SupplierPaymentRowDto> Payments);

/// <param name="BatchesCreated">
/// One per line. Reported back so the success message can say "2 batches added" — a purchase
/// silently creating stock is the part a person most needs confirmed.
/// </param>
/// <param name="Warnings">
/// Non-blocking, one per line that would sell at a loss. The batch-creation command produces
/// these; this module passes them through rather than re-deriving them.
/// </param>
public sealed record PurchaseCreatedDto(
    Guid PurchaseId,
    string PurchaseNumber,
    decimal TotalAmount,
    int BatchesCreated,
    IReadOnlyList<string> Warnings);

// ── Returns ──────────────────────────────────────────────────────────────────────────────

/// <param name="ReturnableInBaseUnits">
/// <c>min(delivered − already returned, what the batch still holds)</c>.
///
/// <para><b>The batch cap is the half that is easy to forget.</b> If stock has been sold or
/// adjusted out, it is not on the shelf to hand back, however much the bill said — and a return
/// that ignored this would take the batch negative or, worse, be refused by the domain with an
/// error the person could not act on.</para>
/// </param>
/// <param name="CappedByStock">
/// True when physical stock, not the bill, is what limits the return. The screen says which,
/// because "you can only return 20 of the 50 you bought" is a different problem to solve
/// depending on the answer.
/// </param>
public sealed record ReturnablePurchaseLineDto(
    Guid PurchaseLineId,
    Guid ProductId,
    string BrandName,
    string? GenericName,
    Guid BatchId,
    string BatchNumber,
    DateOnly? ExpiryDate,
    int DeliveredInBaseUnits,
    string FormattedDelivered,
    int AlreadyReturnedInBaseUnits,
    int BatchQuantityInBaseUnits,
    int ReturnableInBaseUnits,
    string FormattedReturnable,
    decimal PurchasePricePerBaseUnit,
    string BaseUnitName,
    string? MidUnitName,
    string? LargeUnitName,
    int? BasePerMid,
    int? MidPerLarge,
    bool CappedByStock)
{
    public bool CanReturn => ReturnableInBaseUnits > 0;

    /// <summary>What returning everything still returnable would credit.</summary>
    public decimal RefundIfAllReturned => ReturnableInBaseUnits * PurchasePricePerBaseUnit;
}

public sealed record ReturnablePurchaseDto(
    Guid PurchaseId,
    string PurchaseNumber,
    Guid SupplierId,
    string SupplierName,
    IReadOnlyList<ReturnablePurchaseLineDto> Lines);

public sealed record PurchaseReturnedDto(
    Guid ReturnId,
    Guid PurchaseLineId,
    Guid BatchId,
    int QuantityReturnedInBaseUnits,
    string FormattedQuantityReturned,
    decimal ReturnAmount,
    int BatchQuantityAfter,
    string FormattedBatchQuantityAfter,
    decimal SupplierBalanceAfter);
