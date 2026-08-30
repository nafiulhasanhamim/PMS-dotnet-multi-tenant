using Ardalis.GuardClauses;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Mappings;
using PMS.Application.Features.Customers.Specifications;
using PMS.Application.Features.Products.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.ValueObjects;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Orders.Commands.CreateOrder;

/// <summary>
/// Handler for CreateOrderCommand.
/// </summary>
public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result<OrderDto>>
{
    private readonly IRepository<Order, IApplicationDbContext> _orderRepository;
    private readonly IReadRepository<Customer, IApplicationDbContext> _customerRepository;
    private readonly IRepository<Product, IApplicationDbContext> _productRepository;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;

    public CreateOrderCommandHandler(
        IRepository<Order, IApplicationDbContext> orderRepository,
        IReadRepository<Customer, IApplicationDbContext> customerRepository,
        IRepository<Product, IApplicationDbContext> productRepository,
        IUnitOfWork<IApplicationDbContext> unitOfWork)
    {
        _orderRepository = Guard.Against.Null(orderRepository);
        _customerRepository = Guard.Against.Null(customerRepository);
        _productRepository = Guard.Against.Null(productRepository);
        _unitOfWork = Guard.Against.Null(unitOfWork);
    }

    public async Task<Result<OrderDto>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // Verify customer exists
        var customer = await _customerRepository.FirstOrDefaultAsync(
            new CustomerByIdSpec(request.CustomerId), cancellationToken);

        if (customer is null)
        {
            return Result.Failure<OrderDto>(
                Error.NotFound("Customer", request.CustomerId));
        }

        // Create shipping address
        var addressResult = Address.Create(
            request.ShippingAddress.Street,
            request.ShippingAddress.City,
            request.ShippingAddress.State,
            request.ShippingAddress.PostalCode,
            request.ShippingAddress.Country);

        if (addressResult.IsFailure)
        {
            return Result.Failure<OrderDto>(addressResult.Error);
        }

        // Create order
        var order = new Order(request.CustomerId, addressResult.Value, request.Notes);

        // Add items
        foreach (var item in request.Items)
        {
            var product = await _productRepository.FirstOrDefaultAsync(
                new ProductByIdSpec(item.ProductId), cancellationToken);

            if (product is null)
            {
                return Result.Failure<OrderDto>(
                    Error.NotFound("Product", item.ProductId));
            }

            if (product.StockQuantity < item.Quantity)
            {
                return Result.Failure<OrderDto>(
                    Error.Validation("Stock", $"Insufficient stock for product '{product.Name}'. Available: {product.StockQuantity}, Requested: {item.Quantity}"));
            }

            // Reduce stock
            product.RemoveStock(item.Quantity);
            await _productRepository.UpdateAsync(product, cancellationToken);

            // Add order item (unitPrice first, then quantity)
            order.AddItem(product.Id, product.Name, product.Price, item.Quantity);
        }

        await _orderRepository.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return order.ToDto();
    }
}
