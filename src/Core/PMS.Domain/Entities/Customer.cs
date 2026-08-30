using Ardalis.GuardClauses;
using PMS.Domain.Enums;
using PMS.Domain.Events;
using PMS.Domain.Exceptions;
using PMS.Domain.ValueObjects;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// Represents a customer in the system.
/// This is an aggregate root.
/// </summary>
public sealed class Customer : BaseAuditableAggregateRoot<Guid>, ISoftDelete
{
    private readonly List<Order> _orders = [];

    /// <summary>
    /// Gets the customer's first name.
    /// </summary>
    public string FirstName { get; private set; } = null!;

    /// <summary>
    /// Gets the customer's last name.
    /// </summary>
    public string LastName { get; private set; } = null!;

    /// <summary>
    /// Gets the customer's full name.
    /// </summary>
    public string FullName => $"{FirstName} {LastName}";

    /// <summary>
    /// Gets the customer's email address.
    /// </summary>
    public Email Email { get; private set; } = null!;

    /// <summary>
    /// Gets the customer's phone number.
    /// </summary>
    public PhoneNumber? PhoneNumber { get; private set; }

    /// <summary>
    /// Gets the customer's shipping address.
    /// </summary>
    public Address? ShippingAddress { get; private set; }

    /// <summary>
    /// Gets the customer's status.
    /// </summary>
    public CustomerStatus Status { get; private set; }

    /// <summary>
    /// Gets the customer's orders.
    /// </summary>
    public IReadOnlyCollection<Order> Orders => _orders.AsReadOnly();

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTime? DeletedOnUtc { get; set; }

    /// <inheritdoc />
    public string? DeletedBy { get; set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private Customer() { }

    /// <summary>
    /// Creates a new customer.
    /// </summary>
    /// <param name="firstName">The customer's first name.</param>
    /// <param name="lastName">The customer's last name.</param>
    /// <param name="email">The customer's email address.</param>
    public Customer(string firstName, string lastName, Email email)
    {
        Guard.Against.NullOrWhiteSpace(firstName, nameof(firstName));
        Guard.Against.NullOrWhiteSpace(lastName, nameof(lastName));
        Guard.Against.Null(email, nameof(email));

        Id = Guid.NewGuid();
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email;
        Status = CustomerStatus.Active;

        AddDomainEvent(new CustomerCreatedEvent(Id, email.Value));
    }

    /// <summary>
    /// Updates the customer's name.
    /// </summary>
    public void UpdateName(string firstName, string lastName)
    {
        Guard.Against.NullOrWhiteSpace(firstName, nameof(firstName));
        Guard.Against.NullOrWhiteSpace(lastName, nameof(lastName));

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
    }

    /// <summary>
    /// Updates the customer's email address.
    /// </summary>
    public void UpdateEmail(Email newEmail)
    {
        if (Email == newEmail) return;

        var oldEmail = Email.Value;
        Email = newEmail;

        AddDomainEvent(new CustomerEmailChangedEvent(Id, oldEmail, newEmail.Value));
    }

    /// <summary>
    /// Updates the customer's phone number.
    /// </summary>
    public void UpdatePhoneNumber(PhoneNumber? phoneNumber)
    {
        PhoneNumber = phoneNumber;
    }

    /// <summary>
    /// Updates the customer's shipping address.
    /// </summary>
    public void UpdateShippingAddress(Address? address)
    {
        ShippingAddress = address;
    }

    /// <summary>
    /// Activates the customer account.
    /// </summary>
    public void Activate()
    {
        if (Status == CustomerStatus.Active)
            throw new DomainException("Customer is already active.");

        Status = CustomerStatus.Active;
    }

    /// <summary>
    /// Suspends the customer account.
    /// </summary>
    public void Suspend()
    {
        if (Status == CustomerStatus.Suspended)
            throw new DomainException("Customer is already suspended.");

        Status = CustomerStatus.Suspended;
    }

    /// <summary>
    /// Deactivates the customer account.
    /// </summary>
    public void Deactivate()
    {
        if (Status == CustomerStatus.Inactive)
            throw new DomainException("Customer is already inactive.");

        Status = CustomerStatus.Inactive;
    }
}
