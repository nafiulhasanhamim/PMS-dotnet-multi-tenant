using MediatR;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Purchases.Queries.GetPurchaseOrigins;

/// <summary>
/// Which of these batches arrived on a recorded purchase, and which one.
///
/// <para><b>Module 6's retrofit.</b> The expired-stock page offers "Return to supplier" for a
/// batch with a purchase line behind it and "Adjust stock" for one entered through Add Stock,
/// which has no bill to send anything back against.</para>
///
/// <para>Asked for a whole page of batches at once rather than per row: per row is twenty-five
/// round-trips on a screen that shows twenty-five.</para>
///
/// <para><b>A supplier id on the batch is not the same question.</b> Since this module's retrofit,
/// Add Stock lets somebody name a supplier on a batch they entered by hand — that batch has a
/// supplier and no purchase, and there is nothing to return it on.</para>
/// </summary>
public sealed record GetPurchaseOriginsQuery(IReadOnlyList<Guid> BatchIds)
    : IRequest<Result<IReadOnlyDictionary<Guid, PurchaseOriginDto>>>, ITenantScopedRequest;

public sealed class GetPurchaseOriginsQueryHandler
    : IRequestHandler<GetPurchaseOriginsQuery, Result<IReadOnlyDictionary<Guid, PurchaseOriginDto>>>
{
    private readonly IPurchaseQueries _purchases;

    public GetPurchaseOriginsQueryHandler(IPurchaseQueries purchases) => _purchases = purchases;

    public async Task<Result<IReadOnlyDictionary<Guid, PurchaseOriginDto>>> Handle(
        GetPurchaseOriginsQuery request, CancellationToken cancellationToken) =>
        Result.Success(
            await _purchases.GetPurchaseOriginsAsync(request.BatchIds, cancellationToken));
}
