using Ardalis.GuardClauses;
using Ardalis.Specification.EntityFrameworkCore;
using PMS.SharedKernel.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace PMS.Persistence.Repositories;

/// <summary>
/// Read-only repository implementation using Ardalis.Specification.
/// Optimized for CQRS query-side operations with no-tracking by default.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TContext">The DbContext marker interface type.</typeparam>
public class ReadRepository<TEntity, TContext> : RepositoryBase<TEntity>, IReadRepository<TEntity, TContext>
    where TEntity : class
    where TContext : class, IDbContext
{
    public ReadRepository(TContext dbContext) : base(GetDbContext(dbContext))
    {
    }

    private static DbContext GetDbContext(TContext context)
    {
        Guard.Against.Null(context);

        if (context is not DbContext dbContext)
        {
            throw new ArgumentException(
                $"Context must be a DbContext. Got {context.GetType().Name}",
                nameof(context));
        }

        return dbContext;
    }
}

/// <summary>
/// Read-only repository implementation for single-database scenarios.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class ReadRepository<TEntity> : RepositoryBase<TEntity>, IReadRepository<TEntity>
    where TEntity : class
{
    public ReadRepository(DbContext dbContext) : base(Guard.Against.Null(dbContext))
    {
    }
}
