using Ardalis.GuardClauses;
using PMS.Domain.Billing;
using PMS.Domain.Enums;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// One completed transaction at the counter: the invoice the customer walks out with.
///
/// <para><b>Built in a fixed order, and the order is the point.</b> Lines are added, then the
/// discount is applied across them, then the cash settles it. Each step needs the one before
/// it — a line cannot know its share of a discount without the other lines, and the change due
/// cannot be known before the discount. Doing it in this order, in one place, is what keeps
/// <c>Subtotal</c>, the per-line shares and <c>NetTotal</c> from ever disagreeing.</para>
///
/// <para><b>Nothing here is editable afterwards.</b> There is no Update and no re-pricing. A
/// completed sale is a historical fact: the money has moved and the customer has the paper.
/// The corrective paths are a <see cref="SalesReturn"/> for part of it and <see cref="Cancel"/>
/// for all of it, both of which leave the original readable. See the out-of-scope section in
/// <c>docs/05-billing-and-invoice.md</c>.</para>
/// </summary>
public sealed class Sale : BaseAuditableAggregateRoot<Guid>, ITenantEntity
{
    private readonly List<SaleLine> _lines = [];

    // EF materialises through this.
    private Sale()
    {
    }

    /// <summary>
    /// Starts a sale. Lines, discount and cash follow, in that order — see the class remarks.
    /// </summary>
    /// <param name="invoiceNumber">
    /// Allocated by the invoice-number generator, which is what makes it sequential per
    /// pharmacy and safe with two cashiers working at once.
    /// </param>
    public Sale(
        string invoiceNumber,
        Guid cashierUserId,
        DateTime saleDateUtc,
        string? customerName = null,
        string? customerPhone = null)
    {
        Guard.Against.NullOrWhiteSpace(invoiceNumber, nameof(invoiceNumber));
        Guard.Against.Default(cashierUserId, nameof(cashierUserId));

        Id = Guid.NewGuid();
        InvoiceNumber = invoiceNumber.Trim();
        CashierUserId = cashierUserId;
        SaleDate = saleDateUtc;
        CustomerName = Blank(customerName);
        CustomerPhone = Blank(customerPhone);
        Status = SaleStatus.Completed;
    }

    /// <summary>Stamped by the persistence interceptor on insert; never set by a handler.</summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Human-readable and unique within the pharmacy — "INV-000452". Two pharmacies both have
    /// an INV-000001 and neither knows about the other's: the unique index is on
    /// (TenantId, InvoiceNumber), not on the number alone.
    /// </summary>
    public string InvoiceNumber { get; private set; } = null!;

    /// <summary>
    /// When the sale happened, in UTC. Distinct from CreatedOnUtc on purpose: they hold the
    /// same value today and would not if a held bill or an offline counter were ever added.
    /// Reports index and range-filter on this one.
    /// </summary>
    public DateTime SaleDate { get; private set; }

    /// <summary>Who rang it up. An Employee may only list their own sales, by this column.</summary>
    public Guid CashierUserId { get; private set; }

    /// <summary>Sum of the lines' LineTotal, before any discount.</summary>
    public decimal Subtotal { get; private set; }

    /// <summary>How the cashier expressed the discount, or null when there was none.</summary>
    public DiscountType? DiscountType { get; private set; }

    /// <summary>What they typed: 5 for 5%, or 62 for a flat 62 taka. Null when there was none.</summary>
    public decimal? DiscountValue { get; private set; }

    /// <summary>
    /// The taka actually deducted. Computed and stored, because a percentage re-applied to the
    /// subtotal later can round differently, and because the per-line shares have to reconcile
    /// against one fixed figure.
    /// </summary>
    public decimal DiscountAmount { get; private set; }

    /// <summary><see cref="Subtotal"/> less <see cref="DiscountAmount"/>. What was owed.</summary>
    public decimal NetTotal { get; private set; }

    /// <summary>What the customer handed over.</summary>
    public decimal CashReceived { get; private set; }

    /// <summary><see cref="CashReceived"/> less <see cref="NetTotal"/>. What went back.</summary>
    public decimal ChangeGiven { get; private set; }

    public SaleStatus Status { get; private set; }

    public string? CancelledReason { get; private set; }

    public Guid? CancelledByUserId { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    // ── Customer: optional, and deliberately just text ───────────────────────────────────
    //
    // No customer table, no accounts, no history. A pharmacy counter takes a name and a phone
    // number when it takes one at all, usually so a prescription can be traced or somebody can
    // be rung about a return. Modelling that as an entity would invite duplicate records for
    // the same person under three spellings and buy nothing this module needs.

    public string? CustomerName { get; private set; }

    public string? CustomerPhone { get; private set; }

    // ── Prescription: populated only when the sale contains an antibiotic ────────────────

    public string? PatientName { get; private set; }

    public string? PatientPhone { get; private set; }

    public string? DoctorName { get; private set; }

    public string? PrescriptionNumber { get; private set; }

    public DateOnly? PrescriptionDate { get; private set; }

    /// <summary>
    /// The pharmacist's attestation that they saw the prescription. A checkbox rather than an
    /// upload, because that is what actually happens at a counter — and an unticked box on a
    /// sale containing an antibiotic is refused server-side regardless of what the client sent.
    /// </summary>
    public bool PrescriptionVerified { get; private set; }

    public IReadOnlyCollection<SaleLine> Lines => _lines;

    /// <summary>True when any prescription detail was captured. Module 7's register reads this.</summary>
    public bool HasPrescription =>
        PatientName is not null || DoctorName is not null || PrescriptionNumber is not null;

    /// <summary>
    /// Adds one batch's contribution to the sale and returns it.
    ///
    /// <para><paramref name="lineTotal"/> is passed in rather than multiplied out here: when
    /// FEFO splits a cart item across batches, the item's quoted total is apportioned across
    /// the resulting lines by <c>SaleMath.SplitLineTotals</c> so that they sum to what the
    /// screen showed. A line computing its own total from a fractional per-base-unit price
    /// would leave the invoice a paisa out. See that method for the worked case.</para>
    /// </summary>
    public SaleLine AddLine(
        Guid productId,
        Guid batchId,
        int quantityInBaseUnits,
        UnitLevel unitSold,
        decimal unitSalePrice,
        decimal lineTotal)
    {
        Guard.Against.Default(productId, nameof(productId));
        Guard.Against.Default(batchId, nameof(batchId));

        if (quantityInBaseUnits <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantityInBaseUnits), quantityInBaseUnits,
                "A sale line has to move at least one unit.");
        }

        if (DiscountAmount != 0m || CashReceived != 0m)
        {
            // The fixed order exists so the discount can be split across a known set of lines.
            // Adding one afterwards would leave it with no share, and the shares would no
            // longer sum to the discount.
            throw new InvalidOperationException(
                "Lines cannot be added after the discount is applied or the sale is settled.");
        }

        var line = new SaleLine(
            Id, productId, batchId, quantityInBaseUnits, unitSold, unitSalePrice, lineTotal);

        _lines.Add(line);
        Subtotal += lineTotal;

        return line;
    }

    /// <summary>
    /// Applies a bill-level discount and pushes each line's share onto it.
    ///
    /// <para>One discount per sale, on the subtotal. Per-item and stacked discounts are out of
    /// scope, and either would break the single proportional split that makes returns
    /// honest.</para>
    ///
    /// <para>Callers enforce the role cap before getting here — that is policy, and it lives in
    /// the application layer. This enforces the arithmetic: the amount cannot exceed the
    /// subtotal, and the shares reconcile to it exactly.</para>
    /// </summary>
    public void ApplyDiscount(DiscountType type, decimal value)
    {
        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("A discount needs something to come off.");
        }

        var amount = SaleMath.DiscountAmountFor(Subtotal, type, value);

        DiscountType = type;
        DiscountValue = value;
        DiscountAmount = amount;

        var shares = SaleMath.SplitDiscount(_lines.Select(line => line.LineTotal).ToList(), amount);

        for (var i = 0; i < _lines.Count; i++)
        {
            _lines[i].SetDiscountShare(shares[i]);
        }

        // Belt and braces over the residual rule. If this ever fires, the split is wrong and a
        // later partial return would refund the wrong amount — a silent and expensive failure,
        // so it is worth being loud about here instead.
        var reconciled = _lines.Sum(line => line.DiscountShare);

        if (reconciled != amount)
        {
            throw new InvalidOperationException(
                $"Discount shares total {reconciled} but the discount is {amount}. "
                + "The proportional split failed to reconcile.");
        }
    }

    /// <summary>
    /// Takes the cash and works out the change. The last step: it needs the final net total.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Not enough cash. Refused rather than recorded as a part payment — this module sells for
    /// cash only, and a sale that does not balance is one nobody can reconcile later.
    /// </exception>
    public void Settle(decimal cashReceived)
    {
        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("A sale needs at least one line.");
        }

        NetTotal = SaleMath.Round(Subtotal - DiscountAmount);

        if (cashReceived < NetTotal)
        {
            throw new InvalidOperationException(
                $"Cash received ({cashReceived}) is less than the {NetTotal} owed.");
        }

        CashReceived = cashReceived;
        ChangeGiven = SaleMath.Round(cashReceived - NetTotal);
    }

    /// <summary>
    /// Records whatever prescription detail was captured for this sale.
    ///
    /// <para><b>Every field is optional here, and that is a deliberate loosening.</b> How much
    /// detail is required depends on the pharmacy's
    /// <see cref="Domain.Enums.AntibioticPrescriptionMode"/>: none under Off, whatever the
    /// cashier happened to have under Optional, all of it under Required. A sale cannot know
    /// which mode its pharmacy is on, so it is not the thing that should be refusing an
    /// incomplete one — <c>CompleteSaleCommandHandler</c> reads the mode and refuses before
    /// reaching this method.</para>
    ///
    /// <para>Under Optional this is how a partial record survives: a doctor's name and nothing
    /// else is worth more to a later inspection than a blank row.</para>
    /// </summary>
    public void SetPrescription(
        string? patientName,
        string? patientPhone,
        string? doctorName,
        string? prescriptionNumber,
        DateOnly? prescriptionDate,
        bool verified)
    {
        PatientName = Blank(patientName);
        PatientPhone = Blank(patientPhone);
        DoctorName = Blank(doctorName);
        PrescriptionNumber = Blank(prescriptionNumber);
        PrescriptionDate = prescriptionDate;
        PrescriptionVerified = verified;
    }

    /// <summary>
    /// Reverses the whole sale. Admin only, and the caller restores the stock in the same
    /// transaction — this changes the record, not the shelves.
    /// </summary>
    /// <exception cref="InvalidOperationException">Already cancelled.</exception>
    public void Cancel(string reason, Guid cancelledByUserId, DateTime atUtc)
    {
        Guard.Against.NullOrWhiteSpace(reason, nameof(reason));
        Guard.Against.Default(cancelledByUserId, nameof(cancelledByUserId));

        if (Status == SaleStatus.Cancelled)
        {
            throw new InvalidOperationException(
                $"Invoice {InvoiceNumber} was already cancelled on {CancelledAt:yyyy-MM-dd}. "
                + "Cancelling it twice would restore its stock twice.");
        }

        Status = SaleStatus.Cancelled;
        CancelledReason = reason.Trim();
        CancelledByUserId = cancelledByUserId;
        CancelledAt = atUtc;
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
