using Ardalis.Specification.EntityFrameworkCore;
using PMS.Application.Interfaces;
using PMS.Persistence.Contexts;
using PMS.SharedKernel.Interfaces;

namespace PMS.Persistence.Repositories;

/// <summary>
/// Repository implementation specifically for the ApplicationDbContext.
/// Use this for write operations and primary database access.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <example>
/// // In your handler:
/// public class CreateOrderHandler
/// {
///     private readonly IRepository&lt;Sale, IApplicationDbContext&gt; _saleRepo;
///     private readonly IUnitOfWork&lt;IApplicationDbContext&gt; _unitOfWork;
///
///     public async Task Handle(CreateOrderCommand command, CancellationToken ct)
///     {
///         var sale = new Sale(command.SupplierId, command.ShippingAddress);
///         await _saleRepo.AddAsync(sale, ct);
///         await _unitOfWork.SaveChangesAsync(ct);
///     }
/// }
/// </example>
public class ApplicationRepository<TEntity> : RepositoryBase<TEntity>, IRepository<TEntity, IApplicationDbContext>
    where TEntity : class
{
    public ApplicationRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }
}

/// <summary>
/// Read-only repository implementation for the ApplicationDbContext.
/// Use this for query operations on the primary database.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class ApplicationReadRepository<TEntity> : RepositoryBase<TEntity>, IReadRepository<TEntity, IApplicationDbContext>
    where TEntity : class
{
    public ApplicationReadRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }
}
