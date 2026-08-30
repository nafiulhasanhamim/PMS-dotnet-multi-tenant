namespace PMS.SharedKernel.Interfaces;

/// <summary>
/// Base marker interface for DbContext abstraction in Clean Architecture.
/// This allows Application layer to reference DbContext types without
/// depending on Entity Framework Core.
///
/// Each DbContext should have a corresponding marker interface that extends this:
/// - IApplicationDbContext -> ApplicationDbContext (default)
/// - IReportingDbContext -> ReportingDbContext (read replicas)
/// - etc.
///
/// Usage in Application layer:
/// <code>
/// private readonly IRepository&lt;Customer, IApplicationDbContext&gt; _customerRepo;
/// private readonly IUnitOfWork&lt;IApplicationDbContext&gt; _unitOfWork;
/// </code>
///
/// This pattern enables:
/// 1. Multiple database support (different connection strings)
/// 2. Type-safe repository injection (compiler ensures correct context)
/// 3. Zero EF Core dependency in Application/Domain layers
/// </summary>
public interface IDbContext
{
}
