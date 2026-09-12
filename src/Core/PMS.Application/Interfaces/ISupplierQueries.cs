using PMS.Application.Common.DTOs;
using PMS.SharedKernel.Grid;

namespace PMS.Application.Interfaces;

/// <summary>
/// Reads over suppliers, their purchases and their payments.
///
/// <para><b>No method computes a balance itself.</b> Every one that needs one calls
/// <c>ISupplierBalanceQueries</c> — see that interface for why the arithmetic exists in exactly
/// one place, and why the list pages its suppliers first and then asks for balances by id rather
/// than projecting them per row.</para>
///
/// <para>No method takes a tenant id. Every entity read implements <c>ITenantEntity</c>, so the
/// global query filter supplies the pharmacy.</para>
/// </summary>
public interface ISupplierQueries
{
    /// <summary>
    /// One page of suppliers, each with its outstanding balance.
    /// </summary>
    /// <param name="search">Matches name or phone. A pharmacy looks a distributor up by either.</param>
    Task<GridResult<SupplierListItemDto>> GetSuppliersAsync(
        string? search,
        SupplierStatusFilter status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>One supplier with its totals, or null if this pharmacy has no such supplier.</summary>
    Task<SupplierDetailDto?> GetSupplierAsync(
        Guid supplierId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Active suppliers for a dropdown — the "new purchase" picker and, since Module 4's
    /// retrofit, the Add Stock form's supplier field.
    ///
    /// <para>Active only: a deactivated supplier is one the pharmacy has stopped buying from, and
    /// offering it would be offering a mistake. Their existing purchases stay visible everywhere.</para>
    /// </summary>
    Task<IReadOnlyList<SupplierOptionDto>> GetSupplierOptionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>One page of a supplier's purchases, each with its own due and status.</summary>
    Task<GridResult<SupplierPurchaseRowDto>> GetSupplierPurchasesAsync(
        Guid supplierId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of a supplier's payments, newest first.
    ///
    /// <para>Purchase-linked and general payments together, because they are the same event from
    /// the pharmacy's point of view — money went out. The linked purchase number is what
    /// distinguishes them on screen.</para>
    /// </summary>
    Task<GridResult<SupplierPaymentRowDto>> GetSupplierPaymentsAsync(
        Guid supplierId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unpaid and partially paid purchases, for the payment form's "link to a bill" dropdown.
    ///
    /// <para>Settled bills are omitted: a payment against one would be an overpayment recorded in
    /// the least visible possible place. Somebody who genuinely means to do that records a general
    /// payment, which is what that option is for.</para>
    /// </summary>
    Task<IReadOnlyList<SupplierPurchaseRowDto>> GetUnsettledPurchasesAsync(
        Guid supplierId, CancellationToken cancellationToken = default);
}
