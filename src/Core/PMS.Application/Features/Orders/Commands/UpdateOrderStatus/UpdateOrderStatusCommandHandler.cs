using Ardalis.GuardClauses;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Mappings;
using PMS.Application.Features.Orders.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Orders.Commands.UpdateOrderStatus;

/// <summary>
/// Handler for UpdateOrderStatusCommand.
/// </summary>
public sealed class UpdateOrderStatusCommandHandler : IRequestHandler<UpdateOrderStatusCommand, Result<OrderDto>>
{
    private readonly IRepository<Order, IApplicationDbContext> _orderRepository;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;

    public UpdateOrderStatusCommandHandler(
        IRepository<Order, IApplicationDbContext> orderRepository,
        IUnitOfWork<IApplicationDbContext> unitOfWork)
    {
        _orderRepository = Guard.Against.Null(orderRepository);
        _unitOfWork = Guard.Against.Null(unitOfWork);
    }

    public async Task<Result<OrderDto>> Handle(UpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.FirstOrDefaultAsync(
            new OrderByIdSpec(request.Id), cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderDto>(
                Error.NotFound("Order", request.Id));
        }

        // Apply status transition
        var result = request.NewStatus switch
        {
            OrderStatus.Confirmed => TryConfirm(order),
            OrderStatus.Processing => TryStartProcessing(order),
            OrderStatus.Shipped => TryShip(order),
            OrderStatus.Delivered => TryDeliver(order),
            OrderStatus.Cancelled => TryCancel(order),
            _ => Result.Failure(Error.Validation("Status", $"Cannot transition to status '{request.NewStatus}'."))
        };

        if (result.IsFailure)
        {
            return Result.Failure<OrderDto>(result.Error);
        }

        await _orderRepository.UpdateAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return order.ToDto();
    }

    private static Result TryConfirm(Order order)
    {
        if (order.Status != OrderStatus.Pending)
        {
            return Result.Failure(Error.Validation("Status", $"Order can only be confirmed from Pending status. Current status: {order.Status}"));
        }

        order.Confirm();
        return Result.Success();
    }

    private static Result TryStartProcessing(Order order)
    {
        if (order.Status != OrderStatus.Confirmed)
        {
            return Result.Failure(Error.Validation("Status", $"Order can only be processed from Confirmed status. Current status: {order.Status}"));
        }

        order.StartProcessing();
        return Result.Success();
    }

    private static Result TryShip(Order order)
    {
        if (order.Status != OrderStatus.Processing)
        {
            return Result.Failure(Error.Validation("Status", $"Order can only be shipped from Processing status. Current status: {order.Status}"));
        }

        order.Ship();
        return Result.Success();
    }

    private static Result TryDeliver(Order order)
    {
        if (order.Status != OrderStatus.Shipped)
        {
            return Result.Failure(Error.Validation("Status", $"Order can only be delivered from Shipped status. Current status: {order.Status}"));
        }

        order.MarkAsDelivered();
        return Result.Success();
    }

    private static Result TryCancel(Order order)
    {
        if (order.Status == OrderStatus.Delivered || order.Status == OrderStatus.Cancelled)
        {
            return Result.Failure(Error.Validation("Status", $"Order cannot be cancelled from {order.Status} status."));
        }

        order.Cancel();
        return Result.Success();
    }
}
