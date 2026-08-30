using PMS.SharedKernel.Common;

namespace PMS.Domain.Events;

/// <summary>
/// Domain event raised when a customer's email address is changed.
/// </summary>
public sealed class CustomerEmailChangedEvent : DomainEvent
{
    /// <summary>
    /// Gets the ID of the customer.
    /// </summary>
    public Guid CustomerId { get; }

    /// <summary>
    /// Gets the old email address.
    /// </summary>
    public string OldEmail { get; }

    /// <summary>
    /// Gets the new email address.
    /// </summary>
    public string NewEmail { get; }

    public CustomerEmailChangedEvent(Guid customerId, string oldEmail, string newEmail)
    {
        CustomerId = customerId;
        OldEmail = oldEmail;
        NewEmail = newEmail;
    }
}
