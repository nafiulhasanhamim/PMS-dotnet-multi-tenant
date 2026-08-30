using Ardalis.Specification;
using PMS.Domain.Entities;

namespace PMS.Application.Features.Orders.Specifications;

/// <summary>
/// Specification to get orders by customer ID.
/// </summary>
public sealed class OrdersByCustomerSpec : Specification<Order>
{
    public OrdersByCustomerSpec(Guid customerId, int? skip = null, int? take = null)
    {
        Query
            .Where(o => o.CustomerId == customerId)
            .Include(o => o.Items)
            .OrderByDescending(o => o.OrderDateUtc);

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
