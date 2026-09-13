using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

using PMS.Domain.Entities;

namespace PMS.Application.Features.Suppliers.Queries.GetSupplier;

/// <summary>One supplier with its totals.</summary>
public sealed record GetSupplierQuery(Guid SupplierId)
    : IRequest<Result<SupplierDetailDto>>, ITenantScopedRequest;

public sealed class GetSupplierQueryHandler
    : IRequestHandler<GetSupplierQuery, Result<SupplierDetailDto>>
{
    private readonly ISupplierQueries _suppliers;

    public GetSupplierQueryHandler(ISupplierQueries suppliers) => _suppliers = suppliers;

    public async Task<Result<SupplierDetailDto>> Handle(
        GetSupplierQuery request, CancellationToken cancellationToken)
    {
        var supplier = await _suppliers.GetSupplierAsync(request.SupplierId, cancellationToken);

        // Not found rather than forbidden: the tenant filter means another pharmacy's supplier
        // genuinely does not exist here, and saying so leaks nothing about whether it exists
        // elsewhere.
        return supplier is null
            ? Result.Failure<SupplierDetailDto>(
                Error.NotFound(nameof(Supplier), request.SupplierId))
            : Result.Success(supplier);
    }
}
