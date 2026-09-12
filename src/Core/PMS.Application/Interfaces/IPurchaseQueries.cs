using PMS.Application.Common.DTOs;
using PMS.SharedKernel.Grid;

namespace PMS.Application.Interfaces;

/// <summary>
/// Reads over purchases, their lines, and what can still be returned.
///
/// <para>Like <c>ISupplierQueries</c>, no method takes a tenant id and none reimplements the
/// due/status arithmetic — that is <c>PurchaseMath</c>, so the purchases list and the purchase
/// detail page cannot disagree about whether a bill is settled.</para>
/// </summary>
public interface IPurchaseQueries
{
    /// <summary>
    /// One page of purchases.
    /// </summary>
    /// <param name="status">
    /// Filtered <b>after</b> the due is computed, because the status is not a stored column — it
    /// depends on returns booked against the purchase's lines. See the implementation for what
    /// that costs and why it is the honest way round.
    /// </param>
    Task<GridResult<PurchaseListItemDto>> GetPurchasesAsync(
        Guid? supplierId,
        DateOnly? from,
        DateOnly? to,
        PurchaseStatusFilter status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>One purchase with its lines and every payment recorded against it.</summary>
    Task<PurchaseDetailDto?> GetPurchaseAsync(
        Guid purchaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// What can still be sent back from each line of a purchase, already capped by both prior
    /// returns and what the batch physically holds.
    ///
    /// <para>Lines with nothing returnable are <b>included</b>, not filtered out. A screen that
    /// silently omitted them would leave somebody hunting for a product they can see on the
    /// purchase; the page shows them disabled with the reason instead.</para>
    /// </summary>
    Task<ReturnablePurchaseDto?> GetReturnableLinesAsync(
        Guid purchaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Which of these batches came from a recorded purchase, and which purchase.
    ///
    /// <para>Module 6's retrofit: the expired-stock page offers "Return to supplier" for a batch
    /// with a purchase line behind it, and "Adjust stock" for one entered through Add Stock, which
    /// has nobody to send it back to. Asked for a page of batches at once rather than per row,
    /// because per row is N queries on a screen that shows twenty-five.</para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, PurchaseOriginDto>> GetPurchaseOriginsAsync(
        IReadOnlyCollection<Guid> batchIds, CancellationToken cancellationToken = default);
}

/// <summary>Where a batch came from, when it came from a purchase at all.</summary>
public sealed record PurchaseOriginDto(
    Guid PurchaseId,
    string PurchaseNumber,
    Guid PurchaseLineId,
    Guid SupplierId,
    string SupplierName);
