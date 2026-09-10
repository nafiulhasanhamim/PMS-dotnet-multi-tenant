using Ardalis.Specification;
using PMS.Domain.Entities;

namespace PMS.Application.Features.Sales.Specifications;

/// <summary>
/// The cart's products, in one query.
///
/// <para>A cart of eight items would otherwise be eight <c>GetByIdAsync</c> round trips inside
/// the sale transaction, which is where a lock is being held and where latency is least
/// welcome. Tenant scoping is not expressed here and must not be: <c>Product</c> is an
/// <c>ITenantEntity</c>, so the global query filter adds it — which is also what makes an id
/// belonging to another pharmacy come back as simply absent.</para>
/// </summary>
public sealed class ProductsByIdsSpec : Specification<Product>
{
    public ProductsByIdsSpec(IReadOnlyCollection<Guid> ids)
    {
        Query.Where(product => ids.Contains(product.Id));
    }
}
