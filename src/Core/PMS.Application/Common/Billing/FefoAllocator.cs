using PMS.Domain.Entities;

namespace PMS.Application.Common.Billing;

/// <summary>How much of one cart item comes out of one batch.</summary>
/// <param name="Batch">The batch to deduct from. An entity, because the caller has to update it.</param>
/// <param name="QuantityInBaseUnits">What to take from it.</param>
public sealed record BatchTake(Batch Batch, int QuantityInBaseUnits);

/// <summary>
/// The outcome of trying to fill one cart item from a product's sellable batches.
/// </summary>
/// <param name="Takes">
/// One entry per batch touched, in FEFO order. Empty when the request could not be filled.
/// </param>
/// <param name="AvailableInBaseUnits">
/// Everything sellable across every batch. Reported whether or not the allocation succeeded,
/// because when it fails this is the number the cashier needs to see.
/// </param>
public sealed record FefoAllocation(IReadOnlyList<BatchTake> Takes, int AvailableInBaseUnits)
{
    public bool IsSatisfied => Takes.Count > 0;

    /// <summary>Just the quantities, in order — what the line-total split apportions against.</summary>
    public IReadOnlyList<int> Quantities => Takes.Select(take => take.QuantityInBaseUnits).ToList();
}

/// <summary>
/// Walks a product's batches in FEFO order and works out where a sale takes its stock from.
///
/// <para><b>Deliberately separate from the handler, and deliberately pure.</b> Splitting a
/// quantity across batches is the one piece of sale logic with interesting edge cases — an
/// exact fit, a request one unit larger than everything on the shelf, a batch holding zero
/// that should not appear at all — and testing those through a handler would mean a database,
/// a tenant, a user and a cart for each. It takes the batch list the FEFO helper produced and
/// returns a plan; the handler applies it.</para>
///
/// <para><b>The ordering is not this class's business.</b> It trusts the order it is given,
/// which comes from <c>Fefo.GetActiveBatchesFefo</c> or its database equivalent. Re-sorting
/// here would be the second implementation of "earliest expiry first" that the FEFO helper
/// exists to prevent.</para>
/// </summary>
public static class FefoAllocator
{
    /// <summary>
    /// Plans a deduction of <paramref name="neededInBaseUnits"/> across
    /// <paramref name="fefoBatches"/>.
    ///
    /// <para>All or nothing. A cart item that cannot be filled completely produces no takes at
    /// all, rather than a partial plan the caller might apply — half-selling an item and
    /// telling the cashier afterwards is worse than refusing, because the stock has already
    /// moved.</para>
    /// </summary>
    public static FefoAllocation Allocate(
        IReadOnlyList<Batch> fefoBatches, int neededInBaseUnits)
    {
        ArgumentNullException.ThrowIfNull(fefoBatches);

        var available = 0;
        foreach (var batch in fefoBatches)
        {
            available += batch.QuantityInBaseUnits;
        }

        if (neededInBaseUnits <= 0 || available < neededInBaseUnits)
        {
            return new FefoAllocation([], available);
        }

        var takes = new List<BatchTake>();
        var remaining = neededInBaseUnits;

        foreach (var batch in fefoBatches)
        {
            if (remaining == 0)
            {
                break;
            }

            // A depleted batch stays active and stays on the stock page — it is the cost and
            // expiry behind sales that already happened — so it can legitimately appear in a
            // list that has not been filtered. Skipping it here keeps a zero-quantity sale line
            // out of the invoice.
            if (batch.QuantityInBaseUnits <= 0)
            {
                continue;
            }

            var take = Math.Min(remaining, batch.QuantityInBaseUnits);

            takes.Add(new BatchTake(batch, take));
            remaining -= take;
        }

        return remaining == 0
            ? new FefoAllocation(takes, available)

            // Unreachable given the total check above, and returned rather than asserted
            // because the alternative is throwing from a pure function on a condition the
            // caller can already describe better than an exception could.
            : new FefoAllocation([], available);
    }
}
