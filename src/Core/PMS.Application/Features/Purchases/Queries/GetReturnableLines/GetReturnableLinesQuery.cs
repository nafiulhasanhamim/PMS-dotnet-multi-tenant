using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

using PMS.Domain.Entities;

namespace PMS.Application.Features.Purchases.Queries.GetReturnableLines;

/// <summary>
/// What can still go back from each line, capped by prior returns and by what the batch holds.
/// </summary>
public sealed record GetReturnableLinesQuery(Guid PurchaseId)
    : IRequest<Result<ReturnablePurchaseDto>>, ITenantScopedRequest;

public sealed class GetReturnableLinesQueryHandler
    : IRequestHandler<GetReturnableLinesQuery, Result<ReturnablePurchaseDto>>
{
    private readonly IPurchaseQueries _purchases;

    public GetReturnableLinesQueryHandler(IPurchaseQueries purchases) => _purchases = purchases;

    public async Task<Result<ReturnablePurchaseDto>> Handle(
        GetReturnableLinesQuery request, CancellationToken cancellationToken)
    {
        var returnable = await _purchases.GetReturnableLinesAsync(
            request.PurchaseId, cancellationToken);

        return returnable is null
            ? Result.Failure<ReturnablePurchaseDto>(
                Error.NotFound(nameof(Purchase), request.PurchaseId))
            : Result.Success(returnable);
    }
}
