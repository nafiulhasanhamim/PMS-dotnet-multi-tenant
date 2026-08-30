using PMS.Application.Common.DTOs;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Orders.Commands.CreateOrder;

/// <summary>
/// Command to create a new order.
/// </summary>
public sealed record CreateOrderCommand(
    Guid CustomerId,
    CreateOrderAddressDto ShippingAddress,
    List<CreateOrderItemDto> Items,
    string? Notes = null) : IRequest<Result<OrderDto>>;

/// <summary>
/// DTO for order item creation.
/// </summary>
public sealed record CreateOrderItemDto(
    Guid ProductId,
    int Quantity);

/// <summary>
/// DTO for address in order creation.
/// </summary>
public sealed record CreateOrderAddressDto(
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country);
