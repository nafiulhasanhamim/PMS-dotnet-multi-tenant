using Ardalis.Specification.EntityFrameworkCore;
using PMS.Application.Interfaces;
using PMS.Persistence.Contexts;
using PMS.SharedKernel.Interfaces;

namespace PMS.Persistence.Repositories;

/// <summary>
/// Read-only repository implementation for the ReportingDbContext.
/// Use this for read-heavy operations, reporting, and analytics.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>
/// This repository is optimized for read operations:
/// - Uses NoTracking by default (configured in ReportingDbContext)
/// - Connects to a read replica (if configured)
/// - Should NOT be used for write operations
/// </remarks>
/// <example>
/// // In your query handler:
/// public class GetSalesReportHandler
/// {
///     private readonly IReadRepository&lt;Sale, IReportingDbContext&gt; _reportingRepo;
///
///     public async Task&lt;SalesReportDto&gt; Handle(GetSalesReportQuery query, CancellationToken ct)
///     {
///         // Uses read replica for heavy reporting queries
///         var sales = await _reportingRepo.ListAsync(
///             new OrdersByDateRangeSpec(query.StartDate, query.EndDate), ct);
///         return MapToReport(sales);
///     }
/// }
/// </example>
public class ReportingRepository<TEntity> : RepositoryBase<TEntity>, IReadRepository<TEntity, IReportingDbContext>
    where TEntity : class
{
    public ReportingRepository(ReportingDbContext dbContext) : base(dbContext)
    {
    }
}

/// <summary>
/// Full repository for ReportingDbContext (rarely needed - reporting is typically read-only).
/// Only use this if you need to write to the reporting database (e.g., for materialized views).
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class ReportingWriteRepository<TEntity> : RepositoryBase<TEntity>, IRepository<TEntity, IReportingDbContext>
    where TEntity : class
{
    public ReportingWriteRepository(ReportingDbContext dbContext) : base(dbContext)
    {
    }
}
