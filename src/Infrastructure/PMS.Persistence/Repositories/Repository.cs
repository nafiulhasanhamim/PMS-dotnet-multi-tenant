using Ardalis.GuardClauses;
using Ardalis.Specification.EntityFrameworkCore;
using PMS.SharedKernel.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace PMS.Persistence.Repositories;

/// <summary>
/// Generic repository implementation using Ardalis.Specification.
/// Provides full CRUD operations and specification-based querying.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TContext">The DbContext marker interface type.</typeparam>
public class Repository<TEntity, TContext> : RepositoryBase<TEntity>, IRepository<TEntity, TContext>
    where TEntity : class
    where TContext : class, IDbContext
{
    public Repository(TContext dbContext) : base(GetDbContext(dbContext))
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
/// Generic repository implementation for single-database scenarios.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class Repository<TEntity> : RepositoryBase<TEntity>, IRepository<TEntity>
    where TEntity : class
{
    public Repository(DbContext dbContext) : base(Guard.Against.Null(dbContext))
    {
    }
}
