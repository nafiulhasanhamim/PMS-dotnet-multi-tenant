using Ardalis.Specification;
using PMS.Domain.Entities;

namespace PMS.Application.Features.Customers.Specifications;

/// <summary>
/// Specification to get a customer by ID.
/// </summary>
public sealed class CustomerByIdSpec : Specification<Customer>, ISingleResultSpecification<Customer>
{
    public CustomerByIdSpec(Guid customerId)
    {
        Query.Where(c => c.Id == customerId);
    }
}
