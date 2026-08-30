using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Customers.Commands.DeleteCustomer;

/// <summary>
/// Command to delete a customer (soft delete).
/// </summary>
public sealed record DeleteCustomerCommand(Guid Id) : IRequest<Result>;
