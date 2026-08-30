using PMS.Application.Common.DTOs;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Orders.Commands.UpdateOrderStatus;

/// <summary>
/// Command to update order status.
/// </summary>
public sealed record UpdateOrderStatusCommand(
    Guid Id,
    OrderStatus NewStatus) : IRequest<Result<OrderDto>>;
