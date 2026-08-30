using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace PMS.Persistence.Common;

/// <summary>
/// Dispatches domain events to their handlers using MediatR.
/// Integrates with EF Core ChangeTracker to find entities with events.
/// </summary>
public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IMediator _mediator;
    private readonly DbContext _dbContext;

    public DomainEventDispatcher(IMediator mediator, DbContext dbContext)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task DispatchEventsAsync(IEnumerable<DomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task DispatchAndClearEventsAsync(CancellationToken cancellationToken = default)
    {
        var entities = GetEntitiesWithEvents();
        var domainEvents = GetDomainEvents(entities);

        // Clear events before dispatching to prevent duplicate dispatches
        ClearDomainEvents(entities);

        // Dispatch all events
        await DispatchEventsAsync(domainEvents, cancellationToken);
    }

    /// <summary>
    /// Gets all entities from the ChangeTracker that have domain events.
    /// </summary>
    private IEnumerable<BaseEntity<Guid>> GetEntitiesWithEvents()
    {
        return _dbContext.ChangeTracker
            .Entries<BaseEntity<Guid>>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();
    }

    /// <summary>
    /// Extracts all domain events from the given entities.
    /// </summary>
    private static IEnumerable<DomainEvent> GetDomainEvents(IEnumerable<BaseEntity<Guid>> entities)
    {
        return entities
            .SelectMany(e => e.DomainEvents)
            .ToList();
    }

    /// <summary>
    /// Clears domain events from all given entities.
    /// </summary>
    private static void ClearDomainEvents(IEnumerable<BaseEntity<Guid>> entities)
    {
        foreach (var entity in entities)
        {
            entity.ClearDomainEvents();
        }
    }
}

/// <summary>
/// Domain event dispatcher for contexts that implement IApplicationDbContext.
/// </summary>
/// <typeparam name="TContext">The DbContext marker interface type.</typeparam>
public class DomainEventDispatcher<TContext> : IDomainEventDispatcher
    where TContext : class, IDbContext
{
    private readonly IMediator _mediator;
    private readonly DbContext _dbContext;

    public DomainEventDispatcher(IMediator mediator, TContext dbContext)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));

        if (dbContext is not DbContext ctx)
        {
            throw new ArgumentException(
                $"Context must be a DbContext. Got {dbContext.GetType().Name}",
                nameof(dbContext));
        }

        _dbContext = ctx;
    }

    /// <inheritdoc />
    public async Task DispatchEventsAsync(IEnumerable<DomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task DispatchAndClearEventsAsync(CancellationToken cancellationToken = default)
    {
        var entities = GetEntitiesWithEvents();
        var domainEvents = GetDomainEvents(entities);

        // Clear events before dispatching to prevent duplicate dispatches
        ClearDomainEvents(entities);

        // Dispatch all events
        await DispatchEventsAsync(domainEvents, cancellationToken);
    }

    private IEnumerable<BaseEntity<Guid>> GetEntitiesWithEvents()
    {
        return _dbContext.ChangeTracker
            .Entries<BaseEntity<Guid>>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();
    }

    private static IEnumerable<DomainEvent> GetDomainEvents(IEnumerable<BaseEntity<Guid>> entities)
    {
        return entities
            .SelectMany(e => e.DomainEvents)
            .ToList();
    }

    private static void ClearDomainEvents(IEnumerable<BaseEntity<Guid>> entities)
    {
        foreach (var entity in entities)
        {
            entity.ClearDomainEvents();
        }
    }
}
