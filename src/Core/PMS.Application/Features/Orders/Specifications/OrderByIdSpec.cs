using Ardalis.Specification;
using PMS.Domain.Entities;

namespace PMS.Application.Features.Orders.Specifications;

/// <summary>
/// Specification to get an order by ID with items loaded.
/// </summary>
public sealed class OrderByIdSpec : Specification<Order>, ISingleResultSpecification<Order>
{
    public OrderByIdSpec(Guid id)
    {
        Query
            .Where(o => o.Id == id)
            .Include(o => o.Items);
    }
}
