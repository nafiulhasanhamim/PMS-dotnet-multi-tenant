using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

using PMS.Domain.Entities;

namespace PMS.Application.Features.Purchases.Queries.GetPurchase;

/// <summary>One purchase with its lines and the payments recorded against it.</summary>
public sealed record GetPurchaseQuery(Guid PurchaseId)
    : IRequest<Result<PurchaseDetailDto>>, ITenantScopedRequest;

public sealed class GetPurchaseQueryHandler
    : IRequestHandler<GetPurchaseQuery, Result<PurchaseDetailDto>>
{
    private readonly IPurchaseQueries _purchases;

    public GetPurchaseQueryHandler(IPurchaseQueries purchases) => _purchases = purchases;

    public async Task<Result<PurchaseDetailDto>> Handle(
        GetPurchaseQuery request, CancellationToken cancellationToken)
    {
        var purchase = await _purchases.GetPurchaseAsync(request.PurchaseId, cancellationToken);

        return purchase is null
            ? Result.Failure<PurchaseDetailDto>(
                Error.NotFound(nameof(Purchase), request.PurchaseId))
            : Result.Success(purchase);
    }
}
