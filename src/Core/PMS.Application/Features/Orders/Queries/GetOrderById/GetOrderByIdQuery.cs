using PMS.Application.Common.DTOs;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Orders.Queries.GetOrderById;

/// <summary>
/// Query to get an order by ID.
/// </summary>
public sealed record GetOrderByIdQuery(Guid Id) : IRequest<Result<OrderDto>>;
