using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Purchasing;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Suppliers.Queries.GetSuppliers;

/// <summary>One page of suppliers, each with its computed outstanding balance.</summary>
public sealed record GetSuppliersQuery(
    string? Search, SupplierStatusFilter Status, int Page, int PageSize)
    : IRequest<Result<GridResult<SupplierListItemDto>>>, ITenantScopedRequest;

public sealed class GetSuppliersQueryHandler
    : IRequestHandler<GetSuppliersQuery, Result<GridResult<SupplierListItemDto>>>
{
    private readonly ISupplierQueries _suppliers;

    public GetSuppliersQueryHandler(ISupplierQueries suppliers) => _suppliers = suppliers;

    public async Task<Result<GridResult<SupplierListItemDto>>> Handle(
        GetSuppliersQuery request, CancellationToken cancellationToken) =>
        Result.Success(await _suppliers.GetSuppliersAsync(
            request.Search, request.Status,
            SupplierPaging.Page(request.Page), SupplierPaging.Size(request.PageSize),
            cancellationToken));
}
