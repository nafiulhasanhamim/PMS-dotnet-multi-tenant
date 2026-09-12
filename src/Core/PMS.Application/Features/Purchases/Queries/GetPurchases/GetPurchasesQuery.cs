using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Purchasing;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Purchases.Queries.GetPurchases;

/// <summary>One page of purchases, filtered by supplier, date and payment status.</summary>
public sealed record GetPurchasesQuery(
    Guid? SupplierId,
    DateOnly? From,
    DateOnly? To,
    PurchaseStatusFilter Status,
    int Page,
    int PageSize)
    : IRequest<Result<GridResult<PurchaseListItemDto>>>, ITenantScopedRequest;

public sealed class GetPurchasesQueryHandler
    : IRequestHandler<GetPurchasesQuery, Result<GridResult<PurchaseListItemDto>>>
{
    private readonly IPurchaseQueries _purchases;

    public GetPurchasesQueryHandler(IPurchaseQueries purchases) => _purchases = purchases;

    public async Task<Result<GridResult<PurchaseListItemDto>>> Handle(
        GetPurchasesQuery request, CancellationToken cancellationToken)
    {
        // Swapped rather than refused, matching every other date range in the system: somebody
        // who typed the dates the wrong way round wants to see the rows.
        var (from, to) = request.From is { } f && request.To is { } t && f > t
            ? (request.To, request.From)
            : (request.From, request.To);

        return Result.Success(await _purchases.GetPurchasesAsync(
            request.SupplierId, from, to, request.Status,
            SupplierPaging.Page(request.Page), SupplierPaging.Size(request.PageSize),
            cancellationToken));
    }
}
