using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Purchasing;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Suppliers.Queries.GetSupplierPurchases;

/// <summary>One page of a supplier's bills, each with its own due and status.</summary>
public sealed record GetSupplierPurchasesQuery(Guid SupplierId, int Page, int PageSize)
    : IRequest<Result<GridResult<SupplierPurchaseRowDto>>>, ITenantScopedRequest;

public sealed class GetSupplierPurchasesQueryHandler
    : IRequestHandler<GetSupplierPurchasesQuery, Result<GridResult<SupplierPurchaseRowDto>>>
{
    private readonly ISupplierQueries _suppliers;

    public GetSupplierPurchasesQueryHandler(ISupplierQueries suppliers) => _suppliers = suppliers;

    public async Task<Result<GridResult<SupplierPurchaseRowDto>>> Handle(
        GetSupplierPurchasesQuery request, CancellationToken cancellationToken) =>
        Result.Success(await _suppliers.GetSupplierPurchasesAsync(
            request.SupplierId,
            SupplierPaging.Page(request.Page), SupplierPaging.HistorySize(request.PageSize),
            cancellationToken));
}
