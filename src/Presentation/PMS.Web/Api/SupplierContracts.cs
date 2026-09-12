namespace PMS.Web.Api;

// ═══════════════════════════════════════════════════════════════════════════════════════════
// Module 4 wire contracts: suppliers, purchases, payments and returns.
//
// Enum values must match the server's NUMERIC values — the API serialises enums as integers.
//
// No balance is ever a stored field on any of these. What a supplier is owed arrives already
// computed, from the one service on the server that computes it; nothing here recalculates it,
// and a page that subtracted payments from purchases itself would be the second implementation
// this module exists to avoid.
// ═══════════════════════════════════════════════════════════════════════════════════════════

public enum SupplierStatusFilter
{
    Active = 0,
    Inactive = 1,
    All = 2,
}

/// <summary>
/// Which way money moved between the pharmacy and a supplier.
///
/// <para>A direction rather than a negative amount: a table called <em>payments</em> holding
/// receipts would make every sum over it depend on a sign convention invisible from the name.</para>
/// </summary>
public enum SupplierPaymentDirection
{
    /// <summary>Money out, to the supplier. The default, and everything recorded before refunds.</summary>
    Payment = 0,

    /// <summary>Money back, settling a credit the pharmacy was holding.</summary>
    Refund = 1,

    /// <summary>A credit given up. No money moved.</summary>
    WriteOff = 2,
}

public enum PurchasePaymentStatus
{
    Unpaid = 0,
    PartiallyPaid = 1,
    Paid = 2,
}

public enum PurchaseStatusFilter
{
    All = 0,
    Unpaid = 1,
    PartiallyPaid = 2,
    Paid = 3,
}

/// <summary>
/// What a supplier is owed, and the three figures behind it.
///
/// <para><see cref="Outstanding"/> is computed here from the three parts rather than sent as a
/// fourth field — the arithmetic is a subtraction of numbers already on the wire, and deriving it
/// means the parts and the total cannot disagree on screen.</para>
/// </summary>
public sealed record SupplierBalance(
    decimal TotalPurchased,
    decimal TotalReturned,
    decimal TotalPaid,
    decimal TotalRefunded = 0m,
    decimal TotalWrittenOff = 0m)
{
    public static SupplierBalance Zero { get; } = new(0m, 0m, 0m, 0m, 0m);

    /// <summary>
    /// Refunds and write-offs add back, because both undo a credit: the supplier returning the
    /// cash, or the pharmacy giving up on collecting it, leave nothing owed either way.
    /// </summary>
    public decimal Outstanding =>
        TotalPurchased - TotalReturned - TotalPaid + TotalRefunded + TotalWrittenOff;

    public bool IsOwing => Outstanding > 0m;

    /// <summary>
    /// The supplier owes the pharmacy. Reachable by paying a bill and then returning against it,
    /// or by simply overpaying — and every screen says so rather than clamping at zero.
    /// </summary>
    public bool IsOverpaid => Outstanding < 0m;

    public bool IsSettled => Outstanding == 0m;

    /// <summary>What the supplier would have to hand back to settle a credit.</summary>
    public decimal CreditAvailable => IsOverpaid ? -Outstanding : 0m;

    /// <summary>True once any credit has been settled or written off on this account.</summary>
    public bool HasIncoming => TotalRefunded > 0m || TotalWrittenOff > 0m;
}

public sealed record SupplierListItem(
    Guid Id,
    string Name,
    string? ContactPerson,
    string Phone,
    string? Company,
    bool IsActive,
    SupplierBalance Balance);

public sealed record SupplierDetail(
    Guid Id,
    string Name,
    string Phone,
    string? ContactPerson,
    string? Email,
    string? Address,
    string? Company,
    bool IsActive,
    SupplierBalance Balance,
    int PurchaseCount,
    int PaymentCount)
{
    public static SupplierDetail Empty { get; } = new(
        default, string.Empty, string.Empty, null, null, null, null, true,
        SupplierBalance.Zero, 0, 0);
}

public sealed record SupplierOption(Guid Id, string Name, string? Company, string Phone)
{
    /// <summary>What a dropdown shows: the name, with the company after it when they differ.</summary>
    public string Label =>
        string.IsNullOrWhiteSpace(Company) || Company == Name ? Name : $"{Name} ({Company})";
}

/// <param name="GeneralPaymentApplied">
/// Payments made against the account rather than this bill, notionally allocated to it oldest
/// first. <b>Display only — nothing stores it against the purchase.</b> Already inside
/// <paramref name="Due"/>, which is what makes a supplier's bills add up to their outstanding
/// balance instead of contradicting it.
/// </param>
public sealed record SupplierPurchaseRow(
    Guid PurchaseId,
    string PurchaseNumber,
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

    /// <summary>Whether any of this bill's cover came from an unallocated payment.</summary>
    public bool HasGeneralPayment => GeneralPaymentApplied > 0m;

    /// <summary>Whether a credit this bill held has since been refunded or written off.</summary>
    public bool HasCreditSettled => CreditSettled > 0m;
}

public sealed record SupplierPaymentRow(
    Guid Id,
    DateOnly PaymentDate,
    decimal Amount,
    SupplierPaymentDirection Direction,
    Guid? PurchaseId,
    string? PurchaseNumber,
    string PaymentMethod,
    string? Notes,
    string? RecordedByName)
{
    /// <summary>A payment against the account rather than one bill.</summary>
    public bool IsGeneral => PurchaseId is null;

    /// <summary>Money came back, or a credit was given up. Either way the balance rose.</summary>
    public bool IsIncoming => Direction != SupplierPaymentDirection.Payment;

    /// <summary>How the row reads on screen.</summary>
    public string DirectionLabel => Direction switch
    {
        SupplierPaymentDirection.Refund => "Refund received",
        SupplierPaymentDirection.WriteOff => "Credit written off",
        _ => "Payment",
    };
}

/// <param name="ExceedsBalance">
/// A warning, never a refusal — see the API. The page shows it and keeps the payment.
/// </param>
public sealed record PaymentRecorded(
    Guid PaymentId,
    Guid SupplierId,
    decimal Amount,
    SupplierBalance BalanceAfter,
    bool ExceedsBalance,
    string? Warning);

public sealed record CreateSupplierPayload(
    string Name,
    string Phone,
    string? ContactPerson,
    string? Email,
    string? Address,
    string? Company);

public sealed record RecordPaymentPayload(
    Guid? PurchaseId,
    decimal Amount,
    DateOnly? PaymentDate,
    string? PaymentMethod,
    string? Notes,
    SupplierPaymentDirection Direction = SupplierPaymentDirection.Payment);
