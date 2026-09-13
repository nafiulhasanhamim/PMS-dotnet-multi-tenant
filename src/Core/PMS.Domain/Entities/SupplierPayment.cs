using PMS.Domain.Enums;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// Money going out to a supplier.
///
/// <para><b>Two kinds, and the difference is whether anyone knows which bill it settled.</b>
/// A payment with a <see cref="PurchaseId"/> was handed over against a specific invoice, and it
/// advances that purchase's <c>AmountPaid</c>. A payment without one is money against the account
/// — "here's fifty thousand, put it against what we owe" — which is how a great many pharmacy
/// settlements actually happen.</para>
///
/// <para><b>A general payment is never allocated to individual purchases in the data.</b> It
/// reduces the supplier's balance and nothing else. Screens that want to show which bills it
/// probably covered apply it notionally, oldest first, at display time; writing that guess into
/// <c>Purchase.AmountPaid</c> would turn a presentational convention into a stored fact that
/// nobody could later distinguish from a real allocation.</para>
///
/// <para><b>Money can move either way.</b> <see cref="Direction"/> says which. A refund settles a
/// credit the pharmacy is holding — goods sent back after a bill was paid, or a payment that
/// overshot — and a write-off gives that credit up without any cash moving. Both are recorded here
/// rather than as negative payments, so that a sum over <see cref="Amount"/> never depends on a
/// sign convention invisible from the column name.</para>
///
/// <para><b>Immutable once recorded.</b> There is no edit and no delete: a payment is a statement
/// that money left the till on a date. Correcting one means recording the reality — which, for an
/// amount entered wrongly, is a conversation with the supplier rather than a database edit.</para>
/// </summary>
public sealed class SupplierPayment : BaseAuditableAggregateRoot<Guid>, ITenantEntity
{
    // EF materialises through this.
    private SupplierPayment()
    {
    }

    public SupplierPayment(
        Guid supplierId,
        Guid? purchaseId,
        decimal amount,
        DateOnly paymentDate,
        string paymentMethod,
        string? notes,
        Guid recordedByUserId,
        SupplierPaymentDirection direction = SupplierPaymentDirection.Payment)
    {
        Id = Guid.NewGuid();
        SupplierId = supplierId;
        PurchaseId = purchaseId;
        Amount = amount;
        Direction = direction;
        PaymentDate = paymentDate;
        PaymentMethod = paymentMethod.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        RecordedByUserId = recordedByUserId;
    }

    /// <summary>Stamped by the persistence interceptor on insert; never set by a handler.</summary>
    public Guid TenantId { get; set; }

    public Guid SupplierId { get; private set; }

    public Supplier Supplier { get; private set; } = null!;

    /// <summary>
    /// Null for a general payment against the account. See the class remarks.
    ///
    /// <para><b>Always null for a refund or a write-off.</b> A credit belongs to the account
    /// rather than to one bill — it is the net of that supplier's returns and overpayments — and
    /// attaching a refund to a single purchase would mean reducing its <c>AmountPaid</c>, which is
    /// a record of money handed over and must not be rewritten. The screens allocate credits
    /// across bills for display, the same way they allocate general payments.</para>
    /// </summary>
    public Guid? PurchaseId { get; private set; }

    public Purchase? Purchase { get; private set; }

    /// <summary>
    /// Always greater than zero, whichever way the money went. <see cref="Direction"/> carries
    /// the sign so that no query has to remember one.
    /// </summary>
    public decimal Amount { get; private set; }

    /// <summary>
    /// Which way the money moved. See <see cref="SupplierPaymentDirection"/>.
    ///
    /// <para>Defaults to <see cref="SupplierPaymentDirection.Payment"/>, which is also what the
    /// migration backfills, so every row recorded before refunds existed keeps its meaning.</para>
    /// </summary>
    public SupplierPaymentDirection Direction { get; private set; }

    /// <summary>Whether this moved the balance back toward what the pharmacy owes.</summary>
    public bool IsIncoming => Direction != SupplierPaymentDirection.Payment;

    public DateOnly PaymentDate { get; private set; }

    /// <summary>
    /// Free text, "Cash" in practice.
    ///
    /// <para>A string rather than an enum because the column costs nothing today and adding
    /// bKash or a bank transfer later should not need a migration and a deployment. The UI offers
    /// one option; the schema is indifferent.</para>
    /// </summary>
    public string PaymentMethod { get; private set; } = null!;

    public string? Notes { get; private set; }

    /// <summary>
    /// Who recorded it. Admin only — settlement is the one thing a Pharmacist cannot do, and this
    /// column is what makes that rule auditable rather than merely enforced.
    /// </summary>
    public Guid RecordedByUserId { get; private set; }
}
