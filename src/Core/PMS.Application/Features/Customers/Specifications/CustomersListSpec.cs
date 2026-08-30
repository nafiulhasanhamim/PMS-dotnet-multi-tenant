using Ardalis.Specification;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Customers.Specifications;

/// <summary>
/// Specification for listing customers with optional filtering and pagination.
/// </summary>
public sealed class CustomersListSpec : Specification<Customer>
{
    public CustomersListSpec(
        string? searchTerm = null,
        CustomerStatus? status = null,
        int? skip = null,
        int? take = null)
    {
        Query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLowerInvariant();
            Query.Where(c =>
                c.FirstName.ToLower().Contains(term) ||
                c.LastName.ToLower().Contains(term) ||
                c.Email.Value.Contains(term));
        }

        if (status.HasValue)
        {
            Query.Where(c => c.Status == status.Value);
        }

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
