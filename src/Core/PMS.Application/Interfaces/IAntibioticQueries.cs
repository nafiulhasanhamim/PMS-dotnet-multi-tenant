using PMS.Application.Common.DTOs;
using PMS.SharedKernel.Grid;

namespace PMS.Application.Interfaces;

/// <summary>
/// The antibiotic register: a read over sales that already happened.
///
/// <para><b>No entity of its own.</b> Every row is assembled from <c>Sale</c>, <c>SaleLine</c>,
/// <c>Product</c>, <c>SalesReturn</c> and <c>Users</c> — the same lightweight pattern Module 6
/// established. What the module adds to the database is one column on <c>Tenants</c>, and that is
/// a setting rather than a record.</para>
///
/// <para>No method takes a tenant id, and none may: every entity read here except <c>Users</c> is
/// an <c>ITenantEntity</c>, so the global query filter supplies the pharmacy.</para>
/// </summary>
public interface IAntibioticQueries
{
    /// <summary>
    /// One page of the register.
    /// </summary>
    /// <remarks>
    /// <para>A row is included when all of the following hold:</para>
    /// <list type="bullet">
    /// <item><description><c>Product.IsAntibiotic</c> — the pharmacist's confirmed flag, not the
    /// catalogue's machine-derived guess.</description></item>
    /// <item><description><c>Sale.Status == Completed</c>. <b>Cancelled sales are excluded
    /// entirely</b>, not shown struck through: a cancelled sale did not dispense anything, and
    /// Module 8 will treat them the same way. If the two ever disagree the totals stop being
    /// worth reading.</description></item>
    /// <item><description><c>Sale.SaleDate</c> inside the range, inclusive of the whole end
    /// day.</description></item>
    /// </list>
    ///
    /// <para>Sorted by sale date descending, then invoice number, so the order is stable across
    /// pages when several sales share a timestamp.</para>
    /// </remarks>
    /// <param name="doctorName">Partial match. A register is searched by half-remembered names.</param>
    /// <param name="prescriptionStatus">
    /// Filters on whether <em>any</em> prescription detail was captured. Its real use is under
    /// Optional mode, where a pharmacy wants to see how much of its own record-keeping is
    /// actually happening.
    /// </param>
    Task<GridResult<AntibioticRegisterRowDto>> GetRegisterAsync(
        DateOnly from,
        DateOnly to,
        Guid? productId,
        string? doctorName,
        Guid? cashierUserId,
        PrescriptionStatusFilter prescriptionStatus,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Row count and net quantity across the whole filtered set, for the summary line.
    ///
    /// <para>Separate from the page so the figure describes the range rather than the page. A
    /// total that changed as somebody paged through would be worse than no total.</para>
    /// </summary>
    Task<AntibioticRegisterSummaryDto> GetRegisterSummaryAsync(
        DateOnly from,
        DateOnly to,
        Guid? productId,
        string? doctorName,
        Guid? cashierUserId,
        PrescriptionStatusFilter prescriptionStatus,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The whole filtered set, streamed, for the CSV export.
    ///
    /// <para><b>Streamed rather than paged or materialised.</b> A busy pharmacy accumulates
    /// thousands of antibiotic lines a year, and an export is exactly the request that asks for
    /// all of them at once. Buffering the lot into a list to write it out again would hold the
    /// whole year in memory for no reason; the rows are written to the response as they arrive
    /// from the reader.</para>
    /// </summary>
    IAsyncEnumerable<AntibioticRegisterRowDto> StreamRegisterAsync(
        DateOnly from,
        DateOnly to,
        Guid? productId,
        string? doctorName,
        Guid? cashierUserId,
        PrescriptionStatusFilter prescriptionStatus,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Total antibiotic quantity dispensed in one calendar month, net of returns and excluding
    /// cancelled sales. The dashboard card.
    /// </summary>
    Task<AntibioticMonthlySummaryDto> GetMonthlySummaryAsync(
        int month,
        int year,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The register's filter dropdowns: antibiotic products this pharmacy holds, and the
    /// cashiers who appear in its antibiotic sales.
    /// </summary>
    Task<AntibioticFilterOptionsDto> GetFilterOptionsAsync(
        CancellationToken cancellationToken = default);
}
