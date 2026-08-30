using Ardalis.Specification;

namespace PMS.SharedKernel.Interfaces;

/// <summary>
/// Read-only repository interface using Specification pattern.
/// Use this when you only need to query data without modifications.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TContext">The DbContext marker interface type.</typeparam>
public interface IReadRepository<TEntity, TContext> : IReadRepositoryBase<TEntity>
    where TEntity : class
    where TContext : IDbContext
{
}

/// <summary>
/// Full repository interface with read and write operations.
/// Uses Ardalis.Specification for flexible querying.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TContext">The DbContext marker interface type.</typeparam>
/// <example>
/// // In Application layer:
/// public class CreateCustomerHandler
/// {
///     private readonly IRepository&lt;Supplier, IApplicationDbContext&gt; _supplierRepo;
///
///     public async Task Handle(CreateCustomerCommand command, CancellationToken ct)
///     {
///         var supplier = new Supplier(command.Name, command.Email);
///         await _supplierRepo.AddAsync(supplier, ct);
///     }
/// }
/// </example>
public interface IRepository<TEntity, TContext> : IRepositoryBase<TEntity>
    where TEntity : class
    where TContext : IDbContext
{
}

// ═══════════════════════════════════════════════════════════════════════════
// Convenience interfaces for single-database scenarios
// Use these if you only have one database and don't need context type safety
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Read-only repository interface (single database convenience).
/// For multi-database scenarios, use IReadRepository&lt;TEntity, TContext&gt; instead.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IReadRepository<TEntity> : IReadRepositoryBase<TEntity>
    where TEntity : class
{
}

/// <summary>
/// Full repository interface (single database convenience).
/// For multi-database scenarios, use IRepository&lt;TEntity, TContext&gt; instead.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IRepository<TEntity> : IRepositoryBase<TEntity>
    where TEntity : class
{
}
