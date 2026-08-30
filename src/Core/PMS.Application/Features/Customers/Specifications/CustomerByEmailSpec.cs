using Ardalis.Specification;
using PMS.Domain.Entities;

namespace PMS.Application.Features.Customers.Specifications;

/// <summary>
/// Specification to get a customer by email address.
/// </summary>
public sealed class CustomerByEmailSpec : Specification<Customer>, ISingleResultSpecification<Customer>
{
    public CustomerByEmailSpec(string email)
    {
        Query.Where(c => c.Email.Value == email.ToLowerInvariant());
    }
}
