namespace PMS.Web.Api;

// ═══════════════════════════════════════════════════════════════════════════════════════════
// Module 4 wire contracts: purchases, their lines, and returns.
// ═══════════════════════════════════════════════════════════════════════════════════════════

public sealed record PurchaseListItem(
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
    int LineCount)
{
    public bool IsOverpaid => Due < 0m;

    public bool HasGeneralPayment => GeneralPaymentApplied > 0m;

    public bool HasCreditSettled => CreditSettled > 0m;
}

/// <param name="QuantityInBaseUnits">
/// What the bill said, frozen at purchase time — <b>not</b> what the batch holds now. The two
/// diverge as soon as anything sells, and a purchase total that moved with the shelf would
/// restate a supplier's invoice every time a customer bought something.
/// </param>
public sealed record PurchaseLine(
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

public sealed record PurchaseDetail(
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
    IReadOnlyList<PurchaseLine> Lines,
    IReadOnlyList<SupplierPaymentRow> Payments)
{
    public static PurchaseDetail Empty { get; } = new(
        default, string.Empty, default, string.Empty, string.Empty, default,
        0, 0, 0, 0, 0, 0, PurchasePaymentStatus.Unpaid, null, null, default, [], []);

    public bool IsOverpaid => Due < 0m;

    public bool HasGeneralPayment => GeneralPaymentApplied > 0m;

    public bool HasCreditSettled => CreditSettled > 0m;
}

public sealed record PurchaseCreated(
    Guid PurchaseId,
    string PurchaseNumber,
    decimal TotalAmount,
    int BatchesCreated,
    IReadOnlyList<string> Warnings);

// ── Returns ──────────────────────────────────────────────────────────────────────────────

/// <param name="ReturnableInBaseUnits">
/// <c>min(delivered − already returned, what the batch still holds)</c>. Zero is common and not an
/// error: it means the stock has been sold, or the line has already gone back in full.
/// </param>
/// <param name="CappedByStock">
/// True when the shelf rather than the bill is the limit. The page says which, because "you
/// already returned most of this" and "it has been sold" are different problems.
/// </param>
public sealed record ReturnablePurchaseLine(
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

    public decimal RefundIfAllReturned => ReturnableInBaseUnits * PurchasePricePerBaseUnit;

    /// <summary>Why this line cannot be returned from, in words the page can print.</summary>
    public string? BlockedReason => CanReturn
        ? null
        : CappedByStock
            ? "No stock remaining from this batch to return."
            : "Everything on this line has already been returned.";
}

public sealed record ReturnablePurchase(
    Guid PurchaseId,
    string PurchaseNumber,
    Guid SupplierId,
    string SupplierName,
    IReadOnlyList<ReturnablePurchaseLine> Lines)
{
    public static ReturnablePurchase Empty { get; } = new(
        default, string.Empty, default, string.Empty, []);

    public bool AnyReturnable => Lines.Any(l => l.CanReturn);
}

public sealed record PurchaseReturned(
    Guid ReturnId,
    Guid PurchaseLineId,
    Guid BatchId,
    int QuantityReturnedInBaseUnits,
    string FormattedQuantityReturned,
    decimal ReturnAmount,
    int BatchQuantityAfter,
    string FormattedBatchQuantityAfter,
    decimal SupplierBalanceAfter);

// ── Payloads ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One line of a new purchase.
///
/// <para>Quantity and price go up in whatever unit the person was holding, with the level
/// alongside — exactly as Add Stock sends them. The browser does no packing arithmetic: that
/// conversion is the one calculation in this system most likely to be got wrong, and it has one
/// implementation on the server.</para>
/// </summary>
public sealed record CreatePurchaseLinePayload(
    Guid ProductId,
    string BatchNumber,
    DateOnly? ExpiryDate,
    DateOnly? ManufactureDate,
    decimal Quantity,
    UnitLevel QuantityUnit,
    decimal PurchasePrice,
    UnitLevel PurchasePriceUnit,
    string? Notes);

public sealed record CreatePurchasePayload(
    Guid SupplierId,
    DateOnly? PurchaseDate,
    string? Notes,
    IReadOnlyList<CreatePurchaseLinePayload> Lines);

public sealed record CreatePurchaseReturnPayload(
    Guid PurchaseLineId,
    decimal Quantity,
    UnitLevel QuantityUnit,
    string Reason);

/// <summary>
/// Where a batch came from, when a recorded purchase brought it in.
///
/// <para>Module 6's retrofit reads this to decide whether the expired-stock page offers "Return to
/// supplier" or "Adjust stock". <b>Not the same question as "does the batch name a supplier"</b>:
/// since this module, Add Stock lets somebody name one on a batch entered by hand, and that batch
/// has no bill to send anything back against.</para>
/// </summary>
public sealed record PurchaseOrigin(
    Guid PurchaseId,
    string PurchaseNumber,
    Guid PurchaseLineId,
    Guid SupplierId,
    string SupplierName);

// ── Supplier dues (the Module 8 retrofit) ────────────────────────────────────────────────

public sealed record SupplierDuesRow(
    Guid SupplierId,
    string SupplierName,
    string? Company,
    string Phone,
    bool IsActive,
    SupplierBalance Balance,
    SupplierBalance PeriodActivity);

public sealed record SupplierDuesReport(
    DateOnly? From,
    DateOnly? To,
    bool AllTime,
    IReadOnlyList<SupplierDuesRow> Rows)
{
    public static SupplierDuesReport Empty { get; } = new(null, null, true, []);

    public decimal TotalPurchased => Rows.Sum(r => r.PeriodActivity.TotalPurchased);

    public decimal TotalPaid => Rows.Sum(r => r.PeriodActivity.TotalPaid);

    public decimal TotalReturned => Rows.Sum(r => r.PeriodActivity.TotalReturned);

    public decimal TotalOutstanding => Rows.Sum(r => r.Balance.Outstanding);

    public int OwingCount => Rows.Count(r => r.Balance.IsOwing);

    public int OverpaidCount => Rows.Count(r => r.Balance.IsOverpaid);
}
