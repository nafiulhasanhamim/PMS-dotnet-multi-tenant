using Ardalis.Specification;
using PMS.Domain.Entities;

namespace PMS.Application.Features.Sales.Specifications;

/// <summary>
/// The batches a sale touched, in one query and tracked so their quantities can be changed.
///
/// <para>Note what is absent: any filter on <c>IsActive</c> or on expiry. A cancellation and a
/// return both put stock back into the batch it came out of, and that batch may well have
/// expired since — stock returned in December against a pack that expired in November still
/// belongs to that pack, and pretending otherwise would attach it to the wrong expiry date and
/// the wrong purchase cost. FEFO will not offer it for sale again; that is a separate question
/// from where it is recorded.</para>
/// </summary>
public sealed class BatchesByIdsSpec : Specification<Batch>
{
    public BatchesByIdsSpec(IReadOnlyCollection<Guid> ids)
    {
        Query.Where(batch => ids.Contains(batch.Id));
    }
}
