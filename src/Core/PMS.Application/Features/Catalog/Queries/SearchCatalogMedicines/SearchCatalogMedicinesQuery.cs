using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using MediatR;

namespace PMS.Application.Features.Catalog.Queries.SearchCatalogMedicines;

/// <summary>
/// Searches the platform medicine catalogue, forgivingly.
///
/// <para>Tenant-scoped despite reading shared data, and that is not a contradiction: the
/// catalogue itself has no tenant, but each result has to say whether <em>this</em> pharmacy
/// has already imported it. So the query needs a pharmacy even though the rows it searches do
/// not belong to one.</para>
/// </summary>
public sealed record SearchCatalogMedicinesQuery(string Term, int Take = 20)
    : IRequest<CatalogSearchResultDto>, ITenantScopedRequest;
