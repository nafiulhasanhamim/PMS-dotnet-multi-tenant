using Ardalis.Specification;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Orders.Specifications;

/// <summary>
/// Specification for listing orders with optional filtering and pagination.
/// </summary>
public sealed class OrdersListSpec : Specification<Order>
{
    public OrdersListSpec(
        Guid? customerId = null,
        OrderStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? skip = null,
        int? take = null)
    {
        Query.Include(o => o.Items);

        if (customerId.HasValue)
        {
            Query.Where(o => o.CustomerId == customerId.Value);
        }

        if (status.HasValue)
        {
            Query.Where(o => o.Status == status.Value);
        }

        if (fromDate.HasValue)
        {
            Query.Where(o => o.OrderDateUtc >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            Query.Where(o => o.OrderDateUtc <= toDate.Value);
        }

        Query.OrderByDescending(o => o.OrderDateUtc);

        if (skip.HasValue)
        {
            Query.Skip(skip.Value);
        }

        if (take.HasValue)
        {
            Query.Take(take.Value);
        }
    }
}
