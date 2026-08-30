using PMS.Application.Common.DTOs;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Customers.Queries.GetCustomerById;

/// <summary>
/// Query to get a customer by ID.
/// </summary>
public sealed record GetCustomerByIdQuery(Guid Id) : IRequest<Result<CustomerDto>>;
