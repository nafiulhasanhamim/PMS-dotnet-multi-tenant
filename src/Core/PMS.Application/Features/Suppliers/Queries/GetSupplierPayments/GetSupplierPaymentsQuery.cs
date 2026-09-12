using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Purchasing;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Suppliers.Queries.GetSupplierPayments;

/// <summary>One page of a supplier's payments, newest first.</summary>
public sealed record GetSupplierPaymentsQuery(Guid SupplierId, int Page, int PageSize)
    : IRequest<Result<GridResult<SupplierPaymentRowDto>>>, ITenantScopedRequest;

public sealed class GetSupplierPaymentsQueryHandler
    : IRequestHandler<GetSupplierPaymentsQuery, Result<GridResult<SupplierPaymentRowDto>>>
{
    private readonly ISupplierQueries _suppliers;

    public GetSupplierPaymentsQueryHandler(ISupplierQueries suppliers) => _suppliers = suppliers;

    public async Task<Result<GridResult<SupplierPaymentRowDto>>> Handle(
        GetSupplierPaymentsQuery request, CancellationToken cancellationToken) =>
        Result.Success(await _suppliers.GetSupplierPaymentsAsync(
            request.SupplierId,
            SupplierPaging.Page(request.Page), SupplierPaging.HistorySize(request.PageSize),
            cancellationToken));
}
