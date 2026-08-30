using PMS.SharedKernel.Interfaces;

namespace PMS.Application.Interfaces;

/// <summary>
/// Marker interface for the reporting/read-replica database context.
/// Use this for read-heavy operations or when connecting to a read replica.
/// </summary>
/// <remarks>
/// This context is typically used for:
/// - Complex reporting queries
/// - Read replicas (to offload reads from the primary database)
/// - Analytics and dashboards
/// - Data exports
///
/// Example usage in handlers:
/// <code>
/// // Read from reporting database (read replica)
/// private readonly IReadRepository&lt;Order, IReportingDbContext&gt; _reportingRepo;
///
/// // Write to primary database
/// private readonly IRepository&lt;Order, IApplicationDbContext&gt; _primaryRepo;
/// </code>
/// </remarks>
public interface IReportingDbContext : IDbContext
{
}
