using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Catalog.Queries.GetCatalogMedicine;

/// <summary>
/// One catalogue entry, to pre-fill the import review form.
///
/// Tenant-scoped for the same reason as the search: the row is shared, but whether this
/// pharmacy has already imported it is not.
/// </summary>
public sealed record GetCatalogMedicineQuery(int Id)
    : IRequest<Result<CatalogMedicineSearchItemDto>>, ITenantScopedRequest;
