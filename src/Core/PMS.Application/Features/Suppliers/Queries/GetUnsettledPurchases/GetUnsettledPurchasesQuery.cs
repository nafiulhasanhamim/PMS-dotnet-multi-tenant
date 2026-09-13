using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Suppliers.Queries.GetUnsettledPurchases;

/// <summary>
/// Bills that still owe something, for the payment form's "link to a purchase" dropdown.
/// </summary>
public sealed record GetUnsettledPurchasesQuery(Guid SupplierId)
    : IRequest<Result<IReadOnlyList<SupplierPurchaseRowDto>>>, ITenantScopedRequest;

public sealed class GetUnsettledPurchasesQueryHandler
    : IRequestHandler<GetUnsettledPurchasesQuery, Result<IReadOnlyList<SupplierPurchaseRowDto>>>
{
    private readonly ISupplierQueries _suppliers;

    public GetUnsettledPurchasesQueryHandler(ISupplierQueries suppliers) => _suppliers = suppliers;

    public async Task<Result<IReadOnlyList<SupplierPurchaseRowDto>>> Handle(
        GetUnsettledPurchasesQuery request, CancellationToken cancellationToken) =>
        Result.Success(
            await _suppliers.GetUnsettledPurchasesAsync(request.SupplierId, cancellationToken));
}
