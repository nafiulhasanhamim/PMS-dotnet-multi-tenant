using PMS.Application.Common.DTOs;
using PMS.Domain.Entities;
using PMS.SharedKernel.Grid;

namespace PMS.Application.Interfaces;

/// <summary>
/// Reads over the current pharmacy's stock.
///
/// <para>No method takes a tenant id, and none may. <c>Batch</c> and <c>StockAdjustment</c>
/// both implement <c>ITenantEntity</c>, so the global query filter supplies the pharmacy on
/// every query below — including the aggregates, which is what makes two pharmacies holding
/// the same catalogue product see only their own totals.</para>
/// </summary>
public interface IStockQueries
{
    /// <summary>One page of the stock list, one row per product with aggregates across its batches.</summary>
    /// <param name="expiringSoonWindowDays">
    /// How many days ahead counts as "expiring soon". Passed in rather than baked in here:
    /// it belongs to the pharmacy and moves to Settings in a later module.
    /// </param>
    Task<GridResult<StockListItemDto>> ListAsync(
        string? search,
        StockStatusFilter stockStatus,
        ExpiryStatusFilter expiryStatus,
        StockProductTypeFilter productType,
        int expiringSoonWindowDays,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One product's stock in full: summary, active batches in FEFO order, and one page of
    /// depleted batches.
    ///
    /// <para>Returns null when the product does not exist <em>in this pharmacy</em> — which
    /// is what another tenant's product id looks like from here.</para>
    /// </summary>
    Task<ProductStockDto?> GetProductStockAsync(
        Guid productId,
        bool includePurchasePrices,
        int expiringSoonWindowDays,
        int depletedPage,
        int depletedPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>One batch, or null when it is not this pharmacy's.</summary>
    Task<BatchDto?> FindBatchAsync(
        Guid batchId,
        bool includePurchasePrices,
        int expiringSoonWindowDays,
        CancellationToken cancellationToken = default);

    /// <summary>A page of one batch's adjustment history, newest first.</summary>
    Task<GridResult<StockAdjustmentDto>> ListAdjustmentsAsync(
        Guid batchId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <b>The FEFO helper, against the database.</b> Sellable batches for one product in the
    /// order they must be sold — see <c>Fefo</c> for the ordering rules, which this shares
    /// rather than restates.
    ///
    /// <para>Returns entities, not DTOs, because its consumer is Module 5's deduction path:
    /// it needs to take quantity from each batch in turn and persist the result. The stock
    /// detail page happens to want the same order and gets it through
    /// <see cref="GetProductStockAsync"/>.</para>
    /// </summary>
    Task<IReadOnlyList<Batch>> GetActiveBatchesFefoAsync(
        Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether this product already has a batch with that number.
    ///
    /// <paramref name="excludingBatchId"/> lets an edit re-save its own row. The out parameters
    /// on the result say whether the clash still holds stock, because that decides between a
    /// refusal and a warning.
    /// </summary>
    Task<BatchNumberClash?> FindBatchNumberClashAsync(
        Guid productId,
        string batchNumber,
        Guid? excludingBatchId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// An existing batch that already uses a batch number, and how much it holds.
///
/// <para>The quantity is the whole reason this is a record rather than a bool: a clash with
/// live stock is a mistake to refuse, and a clash with a depleted batch is a situation to
/// explain. See the duplicate-batch-number policy in <c>docs/03-batches-and-stock.md</c>.</para>
/// </summary>
public sealed record BatchNumberClash(Guid BatchId, int QuantityInBaseUnits, DateOnly? ExpiryDate)
{
    public bool HasStock => QuantityInBaseUnits > 0;
}
