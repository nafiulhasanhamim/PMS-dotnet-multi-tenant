using PMS.Application.Common.DTOs;
using PMS.Domain.Enums;
using PMS.SharedKernel.Grid;

namespace PMS.Application.Interfaces;

/// <summary>
/// Reads that turn what the stock tables already hold into what a pharmacist has to act on.
///
/// <para><b>No entity of its own, and no writes.</b> Every figure here is derived from
/// <c>Batch</c> and <c>Product</c>. That is why this is one query service rather than a feature
/// folder full of handlers with logic in them — the interesting part is the SQL, and the module
/// doc describes the filters rather than a schema.</para>
///
/// <para>No method takes a tenant id, and none may. <c>Batch</c> and <c>Product</c> both
/// implement <c>ITenantEntity</c>, so the global query filter supplies the pharmacy on every
/// query below, aggregates included — which is what makes one pharmacy's expiring stock
/// invisible in another's counts.</para>
/// </summary>
public interface IAlertQueries
{
    /// <summary>
    /// Batches with stock left whose expiry date falls between today and
    /// <paramref name="daysAhead"/> days from now, soonest first.
    ///
    /// <para><b>A batch with no expiry date is excluded, not treated as far-future.</b> Diapers
    /// and syringes never expire; putting them at the end of an expiry list would be noise, and
    /// putting them at the start would be a lie. See the module doc.</para>
    ///
    /// <para>Already-expired batches are excluded too — they have their own list, and mixing
    /// them in would mean a count called "expiring soon" that included things it is too late to
    /// do anything about.</para>
    /// </summary>
    Task<GridResult<ExpiringBatchDto>> GetExpiringBatchesAsync(
        int daysAhead,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batches with stock left whose expiry date has passed, most overdue first.
    ///
    /// <para>Same null rule: no date means it cannot have passed.</para>
    ///
    /// <para>These are still on the shelf, and that is the point of the list. FEFO already
    /// refuses to sell them, so what remains is a physical job somebody has to do — return them
    /// or write them off.</para>
    /// </summary>
    Task<GridResult<ExpiringBatchDto>> GetExpiredBatchesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Active products whose sellable stock is at or below their reorder level, most critically
    /// low first.
    ///
    /// <para><b>Expired batches do not count towards the total.</b> A product with two hundred
    /// expired tablets and nothing else has nothing to sell, and reporting it as adequately
    /// stocked is how a pharmacy discovers the problem from a customer rather than from this
    /// list.</para>
    ///
    /// <para><b>One statement, not one per product.</b> Reorder levels live on the product and
    /// quantities live on its batches, so the total is a grouped sum over batches referenced
    /// from the product row — which SQL Server reads as a correlated subquery and evaluates as a
    /// single pass. A product with no batches at all needs no special case: a sum over no rows
    /// is zero, which is exactly the out-of-stock row the list exists to show.</para>
    ///
    /// <para>Ordered by the ratio of stock to reorder level, ascending, so the product furthest
    /// below its level leads — not the one with the fewest units. Ten of a product that should
    /// hold twenty is a different situation from ten of one that should hold a thousand.</para>
    /// </summary>
    Task<GridResult<LowStockProductDto>> GetLowStockProductsAsync(
        ProductType? productType,
        LowStockStatusFilter status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The four dashboard counts and the nearest-expiry preview line.
    ///
    /// <para>Four round trips. The two batch counts share one pass with conditional aggregates;
    /// the two product counts cannot, because each product's total is a correlated subquery and
    /// SQL Server will not aggregate over an expression containing one. The preview is a TOP 1.
    /// This runs on every dashboard load and behind the sidebar badge, so it is the one read in
    /// the module worth counting queries for — and the web layer caches it briefly for the badge
    /// rather than paying for it on every page.</para>
    /// </summary>
    Task<AlertSummaryDto> GetAlertSummaryAsync(
        int expiryWindowDays,
        CancellationToken cancellationToken = default);
}
