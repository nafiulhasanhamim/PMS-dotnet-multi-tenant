using PMS.Application.Interfaces;
using PMS.Domain.Enums;

namespace PMS.Application.Common.DTOs;

/// <summary>
/// How much of a bill is still outstanding.
///
/// <para><b>A purchase settled entirely by returns counts as <see cref="Paid"/>.</b> Nothing is
/// owed on it, which is what the status column is read for — a row saying "Unpaid" against a
/// delivery that went back in full would send somebody looking for money to pay.</para>
/// </summary>
public enum PurchasePaymentStatus
{
    Unpaid = 0,
    PartiallyPaid = 1,
    Paid = 2,
}

/// <summary>Filter for the purchases list. <see cref="All"/> is the default.</summary>
public enum PurchaseStatusFilter
{
    All = 0,
    Unpaid = 1,
    PartiallyPaid = 2,
    Paid = 3,
}

/// <summary>Whether a supplier list shows active rows, inactive rows, or both.</summary>
public enum SupplierStatusFilter
{
    Active = 0,
    Inactive = 1,
    All = 2,
}

// ── Suppliers ────────────────────────────────────────────────────────────────────────────

/// <param name="Balance">
/// Computed by <c>ISupplierBalanceQueries</c>, never read from a column. The list fetches its
/// page of suppliers first and then asks for balances for exactly those ids — see that service
/// for why the two are separate round-trips rather than one projection.
/// </param>
public sealed record SupplierListItemDto(
    Guid Id,
    string Name,
    string? ContactPerson,
    string Phone,
    string? Company,
    bool IsActive,
    SupplierBalance Balance);

public sealed record SupplierDetailDto(
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
    int PaymentCount);

/// <summary>A supplier for a dropdown. Active only — you cannot buy from a deactivated one.</summary>
public sealed record SupplierOptionDto(Guid Id, string Name, string? Company, string Phone);

/// <param name="ReturnedAmount">
/// Returns booked against this purchase's own lines. Subtracted from the due, because goods that
/// went back are not goods that were bought.
/// </param>
/// <param name="GeneralPaymentApplied">
/// Payments made against the account rather than this bill, notionally allocated to it oldest
/// first. <b>Display only — never written to the purchase.</b> Included in <paramref name="Due"/>
/// so the bills on a supplier's page add up to that supplier's outstanding balance; without it a
/// settled account showed bills marked Unpaid.
/// </param>
/// <param name="Due">
/// <c>TotalAmount − AmountPaid − ReturnedAmount − GeneralPaymentApplied</c>. Can be negative when
/// a bill was paid in full and then partly returned; the screens say "overpaid" rather than
/// clamping it.
/// </param>
public sealed record SupplierPurchaseRowDto(
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
    int LineCount);

/// <param name="PurchaseNumber">
/// Null for a general payment against the account. The screens render "General payment" there
/// rather than an empty cell, because a blank reads as missing data.
/// </param>
public sealed record SupplierPaymentRowDto(
    Guid Id,
    DateOnly PaymentDate,
    decimal Amount,
    SupplierPaymentDirection Direction,
    Guid? PurchaseId,
    string? PurchaseNumber,
    string PaymentMethod,
    string? Notes,
    string? RecordedByName);

/// <param name="ExceedsBalance">
/// <b>A warning, never a refusal.</b> Overpaying happens — a rounded cash settlement, or an
/// advance against the next delivery — and blocking it would leave somebody unable to record
/// money that has genuinely left the till.
/// </param>
public sealed record PaymentRecordedDto(
    Guid PaymentId,
    Guid SupplierId,
    decimal Amount,
    SupplierBalance BalanceAfter,
    bool ExceedsBalance,
    string? Warning);
