using PMS.SharedKernel.Interfaces;

namespace PMS.SharedKernel.Common;

/// <summary>
/// Base class for aggregate roots that track audit information.
/// Combines aggregate root behavior with auditing capabilities.
/// </summary>
/// <typeparam name="TId">The type of the aggregate identifier.</typeparam>
public abstract class BaseAuditableAggregateRoot<TId> : AggregateRoot<TId>, IAuditable
    where TId : notnull
{
    /// <inheritdoc />
    public DateTime CreatedOnUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedOnUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }
}

/// <summary>
/// Auditable aggregate root with integer identifier (convenience class).
/// </summary>
public abstract class BaseAuditableAggregateRoot : BaseAuditableAggregateRoot<int>
{
}
