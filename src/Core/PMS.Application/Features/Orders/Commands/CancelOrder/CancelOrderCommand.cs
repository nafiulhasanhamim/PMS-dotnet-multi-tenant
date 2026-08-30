using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Orders.Commands.CancelOrder;

/// <summary>
/// Command to cancel an order.
/// </summary>
public sealed record CancelOrderCommand(Guid Id) : IRequest<Result>;
