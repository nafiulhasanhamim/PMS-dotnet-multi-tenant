using PMS.Application.Common.DTOs;
using PMS.Domain.Enums;
using PMS.SharedKernel.Grid;

namespace PMS.Application.Interfaces;

/// <summary>
/// Reads over this pharmacy's sales.
///
/// <para>No method takes a tenant id, and none may. <c>Sale</c>, <c>SaleLine</c> and
/// <c>SalesReturn</c> all implement <c>ITenantEntity</c>, so the global query filter supplies
/// the pharmacy on every query here — which is what makes a sale created in one pharmacy
/// invisible in another even by direct id.</para>
/// </summary>
public interface ISaleQueries
{
    /// <summary>
    /// Products the billing screen may offer, with the reason attached to any it may not.
    ///
    /// <para>Inactive products are excluded outright: deactivating a product is how a pharmacy
    /// says it no longer sells the thing, and offering it greyed out would invite somebody to
    /// reactivate it at the counter. Everything else is returned with a
    /// <see cref="SellableStatus"/> — see that enum for why silence is the wrong answer.</para>
    /// </summary>
    /// <param name="callerMaySellAntibiotics">
    /// False for an Employee, which turns antibiotics into
    /// <see cref="SellableStatus.RequiresPharmacist"/> rather than removing them. The API
    /// refuses the sale as well; this is what makes the screen able to explain it.
    /// </param>
    Task<IReadOnlyList<SellableProductDto>> SearchSellableAsync(
        string? search,
        bool callerMaySellAntibiotics,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>One page of the sales list, newest first.</summary>
    /// <param name="onlyCashierUserId">
    /// Set for an Employee, who sees only their own sales. Applied in the query rather than
    /// filtered afterwards, so the page count is right and there is no way to page past it into
    /// somebody else's takings.
    /// </param>
    Task<GridResult<SaleListItemDto>> ListAsync(
        DateOnly? from,
        DateOnly? to,
        Guid? cashierUserId,
        SaleStatusFilter status,
        Guid? onlyCashierUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One sale in full, or null when it is not this pharmacy's — which is also what another
    /// pharmacy's sale id looks like from here.
    /// </summary>
    Task<SaleDetailDto?> FindAsync(
        Guid saleId, CancellationToken cancellationToken = default);

    /// <summary>The lines of one sale with what remains returnable on each.</summary>
    Task<ReturnableSaleDto?> FindReturnableAsync(
        Guid saleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The cashiers who appear in this pharmacy's sales, for the list filter dropdown.
    ///
    /// <para>Drawn from the sales rather than from the staff list on purpose: a filter offering
    /// somebody with no sales is a dead end, and one that omits a cashier who has since left
    /// would hide their sales from an audit.</para>
    /// </summary>
    Task<IReadOnlyList<CashierOptionDto>> ListCashiersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a sale exists in this pharmacy and its current status, without loading it.
    /// Used by the cancel path to tell "not ours" from "already cancelled".
    /// </summary>
    Task<SaleStatus?> FindStatusAsync(
        Guid saleId, CancellationToken cancellationToken = default);
}
