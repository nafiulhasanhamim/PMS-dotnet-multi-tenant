using PMS.Application.Common.DTOs;
using PMS.Domain.Enums;
using MediatR;

namespace PMS.Application.Features.Orders.Queries.GetOrders;

/// <summary>
/// Query to get a list of orders with optional filtering and pagination.
/// </summary>
public sealed record GetOrdersQuery(
    Guid? CustomerId = null,
    OrderStatus? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 10) : IRequest<GetOrdersResponse>;

/// <summary>
/// Response for GetOrdersQuery.
/// </summary>
public sealed record GetOrdersResponse(
    List<OrderSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
