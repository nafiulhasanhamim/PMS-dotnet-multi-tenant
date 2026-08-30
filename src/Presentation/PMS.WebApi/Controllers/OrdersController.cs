using PMS.Application.Common.DTOs;
using PMS.Application.Features.Orders.Commands.CancelOrder;
using PMS.Application.Features.Orders.Commands.CreateOrder;
using PMS.Application.Features.Orders.Commands.UpdateOrderStatus;
using PMS.Application.Features.Orders.Queries.GetOrderById;
using PMS.Application.Features.Orders.Queries.GetOrders;
using PMS.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace PMS.WebApi.Controllers;

/// <summary>
/// API endpoints for order management.
/// </summary>
public class OrdersController : ApiControllerBase
{
    /// <summary>
    /// Gets a paginated list of orders.
    /// </summary>
    /// <param name="customerId">Optional customer ID filter.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="fromDate">Optional start date filter.</param>
    /// <param name="toDate">Optional end date filter.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="pageSize">Page size (default: 10).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of orders.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(GetOrdersResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] Guid? customerId = null,
        [FromQuery] OrderStatus? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetOrdersQuery(customerId, status, fromDate, toDate, page, pageSize);
        var result = await Mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets an order by ID.
    /// </summary>
    /// <param name="id">The order ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The order details.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(Guid id, CancellationToken cancellationToken = default)
    {
        var query = new GetOrderByIdQuery(id);
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Creates a new order.
    /// </summary>
    /// <param name="request">The order creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created order.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var shippingAddress = new CreateOrderAddressDto(
            request.ShippingAddress.Street,
            request.ShippingAddress.City,
            request.ShippingAddress.State,
            request.ShippingAddress.PostalCode,
            request.ShippingAddress.Country);

        var items = request.Items
            .Select(i => new CreateOrderItemDto(i.ProductId, i.Quantity))
            .ToList();

        var command = new CreateOrderCommand(
            request.CustomerId,
            shippingAddress,
            items,
            request.Notes);

        var result = await Mediator.Send(command, cancellationToken);
        return HandleCreatedResult(result, nameof(GetOrder), o => new { id = o.Id });
    }

    /// <summary>
    /// Updates the status of an order.
    /// </summary>
    /// <param name="id">The order ID.</param>
    /// <param name="request">The status update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated order.</returns>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOrderStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateOrderStatusCommand(id, request.Status);
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Cancels an order.
    /// </summary>
    /// <param name="id">The order ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content if successful.</returns>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelOrder(Guid id, CancellationToken cancellationToken = default)
    {
        var command = new CancelOrderCommand(id);
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult(result);
    }
}

/// <summary>
/// Request model for creating an order.
/// </summary>
public sealed record CreateOrderRequest(
    Guid CustomerId,
    CreateOrderAddressRequest ShippingAddress,
    List<CreateOrderItemRequest> Items,
    string? Notes = null);

/// <summary>
/// Request model for an order item.
/// </summary>
public sealed record CreateOrderItemRequest(
    Guid ProductId,
    int Quantity);

/// <summary>
/// Request model for an address.
/// </summary>
public sealed record CreateOrderAddressRequest(
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country);

/// <summary>
/// Request model for updating order status.
/// </summary>
public sealed record UpdateOrderStatusRequest(OrderStatus Status);
