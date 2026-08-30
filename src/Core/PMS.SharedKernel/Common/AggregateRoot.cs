namespace PMS.SharedKernel.Common;

/// <summary>
/// Base class for aggregate roots.
/// An aggregate root is the entry point to an aggregate and ensures consistency.
/// Only aggregate roots should be directly retrieved from repositories.
/// </summary>
/// <typeparam name="TId">The type of the aggregate identifier.</typeparam>
public abstract class AggregateRoot<TId> : BaseEntity<TId>
    where TId : notnull
{
    /// <summary>
    /// Gets or sets the concurrency token for optimistic concurrency control.
    /// </summary>
    public byte[]? RowVersion { get; protected set; }
}

/// <summary>
/// Aggregate root with integer identifier (convenience class).
/// </summary>
public abstract class AggregateRoot : AggregateRoot<int>
{
}
