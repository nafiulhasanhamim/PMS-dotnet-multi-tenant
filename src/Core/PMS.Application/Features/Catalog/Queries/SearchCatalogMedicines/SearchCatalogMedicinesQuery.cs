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
/// <param name="Page">
/// One-based. <b>Paging happens over a capped result set, not over the whole catalogue.</b>
/// Stage 2 scores candidates in memory, so there is no <c>OFFSET</c> to push down — the
/// handler gathers up to a ceiling, orders, and slices. The result reports the total it
/// actually found, and the UI says "first N matches" rather than implying a complete count.
/// Anyone who needs a medicine beyond that ceiling should narrow the search, which is faster
/// than paging to it anyway.
/// </param>
public sealed record SearchCatalogMedicinesQuery(string Term, int Page = 1, int PageSize = 20)
    : IRequest<CatalogSearchResultDto>, ITenantScopedRequest;
