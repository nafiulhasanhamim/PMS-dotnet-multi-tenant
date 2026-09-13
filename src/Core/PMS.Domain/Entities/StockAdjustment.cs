using PMS.Domain.Enums;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// One non-sale change to a batch's quantity, with the reason someone gave for it.
///
/// <para><b>Only <see cref="Batch.Adjust"/> can create one, and it is the only thing that can
/// change a batch's quantity.</b> The constructor is internal to the domain and the method
/// returns the adjustment it just produced, so "quantity changed with no audit row" is not a
/// mistake a handler can make — there is no code path that mutates the one without
/// constructing the other. The transaction in the handler is what makes them land together;
/// this is what makes them exist together.</para>
///
/// <para><b>Why a separate table at all.</b> A quantity column tells you what the shelf holds
/// now. It cannot tell you that eighty pieces were written off as water damage in July, which
/// is the question an owner asks when the numbers do not match the money. Reason is required
/// and free text: a fixed list would be filled in as "Other" for the cases that actually
/// matter, and the quick-pick buttons on the form cover the common ones without closing the
/// door on the unusual one.</para>
/// </summary>
public sealed class StockAdjustment : BaseAuditableAggregateRoot<Guid>, ITenantEntity
{
    // EF materialises through this.
    private StockAdjustment()
    {
    }

    /// <summary>
    /// Records an adjustment. Internal on purpose: see the class remarks — the only caller is
    /// <see cref="Batch.Adjust"/>, which has just applied the matching quantity change.
    /// </summary>
    internal StockAdjustment(
        Guid batchId,
        AdjustmentType adjustmentType,
        int quantityChangeInBaseUnits,
        int quantityAfterInBaseUnits,
        string reason,
        Guid adjustedByUserId)
    {
        Id = Guid.NewGuid();
        BatchId = batchId;
        AdjustmentType = adjustmentType;
        QuantityChangeInBaseUnits = quantityChangeInBaseUnits;
        QuantityAfterInBaseUnits = quantityAfterInBaseUnits;
        Reason = reason.Trim();
        AdjustedByUserId = adjustedByUserId;
    }

    /// <summary>Stamped by the persistence interceptor on insert; never set by a handler.</summary>
    public Guid TenantId { get; set; }

    public Guid BatchId { get; private set; }

    public AdjustmentType AdjustmentType { get; private set; }

    /// <summary>
    /// The change in base units: positive for an addition, negative for a removal. A
    /// correction is whichever direction the truth lies in.
    ///
    /// <para>Stored as the delta rather than the new total because the delta is what sums:
    /// "how much stock did we write off this quarter" is one SUM over this column, and would
    /// otherwise need every row's predecessor to compute.</para>
    /// </summary>
    public int QuantityChangeInBaseUnits { get; private set; }

    /// <summary>
    /// The quantity the batch held immediately after this adjustment.
    ///
    /// <para>Redundant with the deltas, and kept anyway. Replaying every delta to answer "what
    /// did this batch hold in March?" only works if no row is ever missing, and this column is
    /// what makes a gap visible instead of silently shifting every later figure. It also lets
    /// the history table show a before-and-after without a running total in the query.</para>
    /// </summary>
    public int QuantityAfterInBaseUnits { get; private set; }

    /// <summary>Required. Why the quantity changed, in the words of whoever changed it.</summary>
    public string Reason { get; private set; } = null!;

    /// <summary>
    /// Who made the change. A hard reference to the person, not a name copied at the time:
    /// this is the column an owner follows when a pattern of write-offs points one way.
    /// </summary>
    public Guid AdjustedByUserId { get; private set; }

    /// <summary>The batch this adjusted. Mapped so the history query can read its number.</summary>
    public Batch Batch { get; private set; } = null!;

    /// <summary>The quantity before this adjustment, derived rather than stored.</summary>
    public int QuantityBeforeInBaseUnits => QuantityAfterInBaseUnits - QuantityChangeInBaseUnits;
}
