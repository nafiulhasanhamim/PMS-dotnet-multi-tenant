using Ardalis.GuardClauses;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Mappings;
using PMS.Application.Features.Orders.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Orders.Queries.GetOrderById;

/// <summary>
/// Handler for GetOrderByIdQuery.
/// </summary>
public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, Result<OrderDto>>
{
    private readonly IReadRepository<Order, IApplicationDbContext> _orderRepository;

    public GetOrderByIdQueryHandler(IReadRepository<Order, IApplicationDbContext> orderRepository)
    {
        _orderRepository = Guard.Against.Null(orderRepository);
    }

    public async Task<Result<OrderDto>> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.FirstOrDefaultAsync(
            new OrderByIdSpec(request.Id), cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderDto>(
                Error.NotFound("Order", request.Id));
        }

        return order.ToDto();
    }
}
