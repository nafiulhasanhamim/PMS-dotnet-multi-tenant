using Ardalis.Specification;
using PMS.Domain.Entities;

namespace PMS.Application.Features.Products.Specifications;

/// <summary>
/// Specification for listing products with optional filtering and pagination.
/// </summary>
public sealed class ProductsListSpec : Specification<Product>
{
    public ProductsListSpec(
        string? searchTerm = null,
        bool? isActive = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        int? skip = null,
        int? take = null)
    {
        Query.OrderBy(p => p.Name);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLowerInvariant();
            Query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.Sku.ToLower().Contains(term) ||
                (p.Description != null && p.Description.ToLower().Contains(term)));
        }

        if (isActive.HasValue)
        {
            Query.Where(p => p.IsActive == isActive.Value);
        }

        if (minPrice.HasValue)
        {
            Query.Where(p => p.Price.Amount >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            Query.Where(p => p.Price.Amount <= maxPrice.Value);
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
