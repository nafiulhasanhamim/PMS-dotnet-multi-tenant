using Ardalis.GuardClauses;
using PMS.SharedKernel.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace PMS.Persistence.Common;

/// <summary>
/// Unit of Work pattern implementation for managing database transactions.
/// Coordinates work across multiple repositories sharing the same DbContext.
/// </summary>
/// <typeparam name="TContext">The DbContext marker interface type.</typeparam>
public class UnitOfWork<TContext> : IUnitOfWork<TContext>
    where TContext : class, IDbContext
{
    private readonly DbContext _dbContext;
    private IDbContextTransaction? _transaction;
    private bool _disposed;

    public UnitOfWork(TContext dbContext)
    {
        Guard.Against.Null(dbContext);

        if (dbContext is not DbContext ctx)
        {
            throw new ArgumentException(
                $"Context must be a DbContext. Got {dbContext.GetType().Name}",
                nameof(dbContext));
        }

        _dbContext = ctx;
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>Unusable as this context is configured, and it throws rather than misbehaving.</b>
    /// ApplicationDbContext enables retry-on-failure, and
    /// <c>SqlServerRetryingExecutionStrategy</c> refuses a user-initiated transaction: it
    /// cannot retry a block whose boundaries it does not control. Calling this produces
    /// "The configured execution strategy ... does not support user-initiated transactions".
    ///
    /// <para>Nothing in the application uses it. A single <see cref="SaveChangesAsync"/> is
    /// already atomic across every pending change and is retriable, which covers the cases so
    /// far — see AdjustBatchCommandHandler, where one save writes both a batch update and its
    /// audit row. Work that genuinely needs several saves in one transaction has to go
    /// through <c>Database.CreateExecutionStrategy().ExecuteAsync(...)</c>, and the retried
    /// block must be safe to run twice.</para>
    /// </remarks>
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            return;
        }

        _transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    /// <inheritdoc />
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _transaction?.Dispose();
            _transaction = null;
        }

        _disposed = true;
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}

/// <summary>
/// Unit of Work implementation for single-database scenarios.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly DbContext _dbContext;
    private IDbContextTransaction? _transaction;
    private bool _disposed;

    public UnitOfWork(DbContext dbContext)
    {
        _dbContext = Guard.Against.Null(dbContext);
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            return;
        }

        _transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    /// <inheritdoc />
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _transaction?.Dispose();
            _transaction = null;
        }

        _disposed = true;
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
