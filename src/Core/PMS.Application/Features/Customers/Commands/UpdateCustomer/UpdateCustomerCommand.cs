using PMS.Application.Common.DTOs;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Customers.Commands.UpdateCustomer;

/// <summary>
/// Command to update an existing customer.
/// </summary>
public sealed record UpdateCustomerCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber = null) : IRequest<Result<CustomerDto>>;
