using Ardalis.GuardClauses;
using PMS.Application.Features.Orders.Specifications;
using PMS.Application.Features.Products.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Orders.Commands.CancelOrder;

/// <summary>
/// Handler for CancelOrderCommand.
/// </summary>
public sealed class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, Result>
{
    private readonly IRepository<Order, IApplicationDbContext> _orderRepository;
    private readonly IRepository<Product, IApplicationDbContext> _productRepository;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;

    public CancelOrderCommandHandler(
        IRepository<Order, IApplicationDbContext> orderRepository,
        IRepository<Product, IApplicationDbContext> productRepository,
        IUnitOfWork<IApplicationDbContext> unitOfWork)
    {
        _orderRepository = Guard.Against.Null(orderRepository);
        _productRepository = Guard.Against.Null(productRepository);
        _unitOfWork = Guard.Against.Null(unitOfWork);
    }

    public async Task<Result> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.FirstOrDefaultAsync(
            new OrderByIdSpec(request.Id), cancellationToken);

        if (order is null)
        {
            return Result.Failure(
                Error.NotFound("Order", request.Id));
        }

        if (order.Status == OrderStatus.Delivered || order.Status == OrderStatus.Cancelled)
        {
            return Result.Failure(
                Error.Validation("Status", $"Order cannot be cancelled from {order.Status} status."));
        }

        // Restore stock for each item
        foreach (var item in order.Items)
        {
            var product = await _productRepository.FirstOrDefaultAsync(
                new ProductByIdSpec(item.ProductId), cancellationToken);

            if (product is not null)
            {
                product.AddStock(item.Quantity);
                await _productRepository.UpdateAsync(product, cancellationToken);
            }
        }

        order.Cancel();
        await _orderRepository.UpdateAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
