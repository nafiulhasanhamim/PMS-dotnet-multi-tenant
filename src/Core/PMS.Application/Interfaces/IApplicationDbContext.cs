using PMS.SharedKernel.Interfaces;

namespace PMS.Application.Interfaces;

/// <summary>
/// Marker interface for the main application database context.
/// Used as a type parameter in repositories and unit of work to specify
/// which database to use.
///
/// Implementation: ApplicationDbContext in Persistence layer
///
/// Usage:
/// <code>
/// public class GetCustomersHandler
/// {
///     private readonly IRepository&lt;Supplier, IApplicationDbContext&gt; _supplierRepo;
///     private readonly IUnitOfWork&lt;IApplicationDbContext&gt; _unitOfWork;
/// }
/// </code>
///
/// For additional databases, create more marker interfaces:
/// - IReportingDbContext -> For read replicas / reporting database
/// - IAuditDbContext -> For audit log database
/// - etc.
/// </summary>
public interface IApplicationDbContext : IDbContext
{
}
