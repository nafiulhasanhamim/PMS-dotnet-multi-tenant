using PMS.SharedKernel.Common;

namespace PMS.Domain.Events;

/// <summary>
/// Domain event raised when a new customer is created.
/// </summary>
public sealed class CustomerCreatedEvent : DomainEvent
{
    /// <summary>
    /// Gets the ID of the created customer.
    /// </summary>
    public Guid CustomerId { get; }

    /// <summary>
    /// Gets the email of the created customer.
    /// </summary>
    public string Email { get; }

    public CustomerCreatedEvent(Guid customerId, string email)
    {
        CustomerId = customerId;
        Email = email;
    }
}
