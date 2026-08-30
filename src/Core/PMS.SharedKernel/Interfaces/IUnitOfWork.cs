namespace PMS.SharedKernel.Interfaces;

/// <summary>
/// Unit of Work pattern interface with DbContext type parameter.
/// Coordinates the work of multiple repositories by ensuring they share
/// a single database context and transaction.
/// </summary>
/// <typeparam name="TContext">The DbContext marker interface type.</typeparam>
/// <example>
/// // In Application layer:
/// public class CreateOrderHandler
/// {
///     private readonly IRepository&lt;Sale, IApplicationDbContext&gt; _saleRepo;
///     private readonly IRepository&lt;Inventory, IApplicationDbContext&gt; _inventoryRepo;
///     private readonly IUnitOfWork&lt;IApplicationDbContext&gt; _unitOfWork;
///
///     public async Task Handle(CreateOrderCommand command, CancellationToken ct)
///     {
///         await _unitOfWork.BeginTransactionAsync(ct);
///         try
///         {
///             await _saleRepo.AddAsync(sale, ct);
///             await _inventoryRepo.UpdateAsync(inventory, ct);
///             await _unitOfWork.SaveChangesAsync(ct);
///             await _unitOfWork.CommitTransactionAsync(ct);
///         }
///         catch
///         {
///             await _unitOfWork.RollbackTransactionAsync(ct);
///             throw;
///         }
///     }
/// }
/// </example>
public interface IUnitOfWork<TContext> : IDisposable, IAsyncDisposable
    where TContext : IDbContext
{
    /// <summary>
    /// Saves all changes made through the repositories to the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a database transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

// ═══════════════════════════════════════════════════════════════════════════
// Convenience interface for single-database scenarios
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Unit of Work pattern interface (single database convenience).
/// For multi-database scenarios, use IUnitOfWork&lt;TContext&gt; instead.
/// </summary>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Saves all changes made through the repositories to the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a database transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
