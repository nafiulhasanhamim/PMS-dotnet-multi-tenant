using Ardalis.Specification;
using PMS.Domain.Entities;

namespace PMS.Application.Features.Products.Specifications;

/// <summary>
/// Specification to get a product by SKU.
/// </summary>
public sealed class ProductBySkuSpec : Specification<Product>, ISingleResultSpecification<Product>
{
    public ProductBySkuSpec(string sku)
    {
        Query.Where(p => p.Sku == sku.ToUpperInvariant());
    }
}
