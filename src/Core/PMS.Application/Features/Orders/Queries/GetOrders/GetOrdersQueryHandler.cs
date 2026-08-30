using Ardalis.GuardClauses;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Mappings;
using PMS.Application.Features.Orders.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using MediatR;

namespace PMS.Application.Features.Orders.Queries.GetOrders;

/// <summary>
/// Handler for GetOrdersQuery.
/// </summary>
public sealed class GetOrdersQueryHandler : IRequestHandler<GetOrdersQuery, GetOrdersResponse>
{
    private readonly IReadRepository<Order, IApplicationDbContext> _orderRepository;

    public GetOrdersQueryHandler(IReadRepository<Order, IApplicationDbContext> orderRepository)
    {
        _orderRepository = Guard.Against.Null(orderRepository);
    }

    public async Task<GetOrdersResponse> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
    {
        // Get total count
        var countSpec = new OrdersListSpec(
            request.CustomerId,
            request.Status,
            request.FromDate,
            request.ToDate);
        var totalCount = await _orderRepository.CountAsync(countSpec, cancellationToken);

        // Get paginated results
        var skip = (request.Page - 1) * request.PageSize;
        var spec = new OrdersListSpec(
            request.CustomerId,
            request.Status,
            request.FromDate,
            request.ToDate,
            skip,
            request.PageSize);
        var orders = await _orderRepository.ListAsync(spec, cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new GetOrdersResponse(
            orders.Select(o => o.ToSummaryDto()).ToList(),
            totalCount,
            request.Page,
            request.PageSize,
            totalPages);
    }
}
