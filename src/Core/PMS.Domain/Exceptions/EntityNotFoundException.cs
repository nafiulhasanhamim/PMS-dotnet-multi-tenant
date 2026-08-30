namespace PMS.Domain.Exceptions;

/// <summary>
/// Exception thrown when a requested entity is not found.
/// </summary>
public class EntityNotFoundException : DomainException
{
    /// <summary>
    /// Gets the type of entity that was not found.
    /// </summary>
    public string EntityType { get; }

    /// <summary>
    /// Gets the identifier that was searched for.
    /// </summary>
    public object EntityId { get; }

    /// <summary>
    /// Creates a new entity not found exception.
    /// </summary>
    /// <param name="entityType">The type of entity.</param>
    /// <param name="entityId">The entity identifier.</param>
    public EntityNotFoundException(string entityType, object entityId)
        : base($"{entityType} with id '{entityId}' was not found.")
    {
        EntityType = entityType;
        EntityId = entityId;
    }

    /// <summary>
    /// Creates a new entity not found exception for a specific entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entityId">The entity identifier.</param>
    public static EntityNotFoundException For<TEntity>(object entityId)
    {
        return new EntityNotFoundException(typeof(TEntity).Name, entityId);
    }
}
