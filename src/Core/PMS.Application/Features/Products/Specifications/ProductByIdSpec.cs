using Ardalis.Specification;
using PMS.Domain.Entities;

namespace PMS.Application.Features.Products.Specifications;

/// <summary>
/// Specification to get a product by ID.
/// </summary>
public sealed class ProductByIdSpec : Specification<Product>, ISingleResultSpecification<Product>
{
    public ProductByIdSpec(Guid productId)
    {
        Query.Where(p => p.Id == productId);
    }
}
