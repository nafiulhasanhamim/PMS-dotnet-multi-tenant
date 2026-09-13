namespace PMS.Web.Api;

// ═══════════════════════════════════════════════════════════════════════════════════════════
// Module 5 wire contracts.
//
// Enum values must match the server's NUMERIC values — the API serialises enums as integers,
// the convention Module 1 established and every client here relies on.
// ═══════════════════════════════════════════════════════════════════════════════════════════

/// <summary>How a bill-level discount was expressed.</summary>
public enum DiscountType
{
    Percent = 0,
    Flat = 1,
}

/// <summary>Whether a sale still counts. There is no "edited" — see the module docs.</summary>
public enum SaleStatus
{
    Completed = 0,
    Cancelled = 1,
}

/// <summary>Status filter on the sales list.</summary>
public enum SaleStatusFilter
{
    All = 0,
    Completed = 1,
    Cancelled = 2,
}

/// <summary>
/// Why a product cannot go in the cart, or that it can.
///
/// <para>The billing screen renders every one of these: an unsellable product appears greyed
/// with its reason rather than being missing, because a cashier who sees nothing concludes the
/// pharmacy does not stock the thing.</para>
/// </summary>
public enum SellableStatus
{
    Sellable = 0,
    SetupIncomplete = 1,
    OutOfStock = 2,
    AllStockExpired = 3,
    RequiresPharmacist = 4,
}

/// <summary>
/// One row of the billing screen's type-ahead: everything a cart line needs to price itself
/// without a second request.
/// </summary>
public sealed record SellableProduct(
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

/// <summary>One cart row on its way to the API. No price and no batch — see the command.</summary>
public sealed record CartItemPayload(Guid ProductId, decimal Quantity, UnitLevel UnitLevel);

/// <summary>The prescription block, sent only when the cart holds an antibiotic.</summary>
public sealed record PrescriptionPayload(
    string? PatientName,
    string? PatientPhone,
    string? DoctorName,
    string? PrescriptionNumber,
    DateOnly? PrescriptionDate,
    bool PrescriptionVerified);

public sealed record CompleteSalePayload(
    IReadOnlyList<CartItemPayload> Items,
    DiscountType? DiscountType,
    decimal? DiscountValue,
    decimal CashReceived,
    string? CustomerName,
    string? CustomerPhone,
    PrescriptionPayload? Prescription);

public sealed record SaleCompleted(
    Guid Id,
    string InvoiceNumber,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal NetTotal,
    decimal CashReceived,
    decimal ChangeGiven,
    int LineCount);

public sealed record SaleListItem(
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
    bool HasReturns)
{
    public bool IsCancelled => Status == SaleStatus.Cancelled;
}

public sealed record SaleLineModel(
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
    decimal RefundedAmount);

/// <summary>
/// One line as the customer sees it: a FEFO split merged back into what they bought.
/// </summary>
public sealed record InvoiceLine(
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

public sealed record PrescriptionModel(
    string? PatientName,
    string? PatientPhone,
    string? DoctorName,
    string? PrescriptionNumber,
    DateOnly? PrescriptionDate,
    bool PrescriptionVerified);

public sealed record SaleDetail(
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
    PrescriptionModel? Prescription,
    IReadOnlyList<InvoiceLine> InvoiceLines,
    IReadOnlyList<SaleLineModel> Lines,
    decimal TotalRefunded)
{
    /// <summary>"Discount (5%)" — how the cashier expressed it, not how it was applied.</summary>
    public string DiscountLabel => DiscountType switch
    {
        Api.DiscountType.Percent => $"Discount ({DiscountValue:0.##}%)",
        _ => "Discount",
    };

    public bool IsCancelled => Status == SaleStatus.Cancelled;
}

/// <param name="RefundIfAllReturned">
/// Computed server-side with the same function the command uses, so the figure shown before
/// confirming is the figure that gets paid.
/// </param>
public sealed record ReturnableLine(
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

public sealed record ReturnableSale(
    Guid SaleId,
    string InvoiceNumber,
    DateTime SaleDate,
    SaleStatus Status,
    decimal NetTotal,
    decimal TotalRefunded,
    IReadOnlyList<ReturnableLine> Lines);

public sealed record CreateReturnPayload(
    Guid SaleLineId,
    decimal Quantity,
    UnitLevel QuantityUnit,
    string Reason);

public sealed record SalesReturned(
    Guid Id,
    Guid SaleLineId,
    string InvoiceNumber,
    string BrandName,
    int QuantityReturnedInBaseUnits,
    string FormattedQuantity,
    decimal RefundAmount,
    string BatchNumber,
    int BatchQuantityAfter);

public sealed record CancelSalePayload(string Reason);

public sealed record CancelledSale(
    Guid SaleId,
    string InvoiceNumber,
    decimal NetTotal,
    int LinesRestored,
    int RestoredInBaseUnits,
    int SkippedAlreadyReturned);

public sealed record CashierOption(Guid UserId, string Name, int SaleCount);

/// <summary>
/// What this cashier may do, read from the API rather than hard-coded here.
///
/// <para>PMS.Web references no other project, so a discount cap written into this app would be
/// a second copy of a number the API enforces — and the copy would go stale the first time
/// somebody changed the real one. See BillingLimitsDto on the server.</para>
/// </summary>
public sealed record BillingLimits(
    decimal? MaxDiscountPercent,
    string CapDescription,
    bool MaySellAntibiotics,
    bool MayReturn,
    bool MayCancel,
    AntibioticPrescriptionMode AntibioticMode)
{
    /// <summary>
    /// A safe default when the call fails: no discount allowed, nothing permitted — and the
    /// loosest antibiotic mode.
    ///
    /// <para>The asymmetry is deliberate. Falling back to no permissions is safe because it only
    /// hides controls the server would refuse anyway. Falling back to <c>Required</c> would be
    /// the opposite: it would render a mandatory prescription panel at a pharmacy that does not
    /// collect one, and the cashier would have to invent data to get past a screen that was wrong
    /// about the rules.</para>
    /// </summary>
    public static BillingLimits None { get; } =
        new(0m, "Max discount: 0%", false, false, false, AntibioticPrescriptionMode.Off);

    /// <summary>Whether the billing screen should render a prescription panel at all.</summary>
    public bool CapturesPrescriptions => AntibioticMode != AntibioticPrescriptionMode.Off;

    /// <summary>Whether every prescription field has to be filled before the sale can complete.</summary>
    public bool RequiresPrescription => AntibioticMode == AntibioticPrescriptionMode.Required;
}
