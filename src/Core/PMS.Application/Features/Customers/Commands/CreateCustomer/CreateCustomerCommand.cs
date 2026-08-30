using PMS.Application.Common.DTOs;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Customers.Commands.CreateCustomer;

/// <summary>
/// Command to create a new customer.
/// </summary>
public sealed record CreateCustomerCommand(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber = null) : IRequest<Result<CustomerDto>>;
