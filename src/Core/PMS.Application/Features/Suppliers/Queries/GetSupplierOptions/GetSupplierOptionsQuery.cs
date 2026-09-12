using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Suppliers.Queries.GetSupplierOptions;

/// <summary>
/// Active suppliers for a dropdown: the new-purchase picker, and since Module 4's retrofit the
/// Add Stock form's supplier field.
/// </summary>
public sealed record GetSupplierOptionsQuery
    : IRequest<Result<IReadOnlyList<SupplierOptionDto>>>, ITenantScopedRequest;

public sealed class GetSupplierOptionsQueryHandler
    : IRequestHandler<GetSupplierOptionsQuery, Result<IReadOnlyList<SupplierOptionDto>>>
{
    private readonly ISupplierQueries _suppliers;

    public GetSupplierOptionsQueryHandler(ISupplierQueries suppliers) => _suppliers = suppliers;

    public async Task<Result<IReadOnlyList<SupplierOptionDto>>> Handle(
        GetSupplierOptionsQuery request, CancellationToken cancellationToken) =>
        Result.Success(await _suppliers.GetSupplierOptionsAsync(cancellationToken));
}
