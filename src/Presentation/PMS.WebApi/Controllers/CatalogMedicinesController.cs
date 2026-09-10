using PMS.Application.Common.DTOs;
using PMS.Application.Features.Catalog.Queries.GetCatalogMedicine;
using PMS.Application.Features.Catalog.Queries.SearchCatalogMedicines;
using PMS.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PMS.WebApi.Controllers;

/// <summary>
/// Search over the platform medicine catalogue, for the import screen.
///
/// <para>The catalogue is shared platform data with no tenant, so these reads are not
/// restricted to one pharmacy - and they must not be. What IS tenant-scoped is the
/// "already in your catalogue" flag on each result, which is why the queries behind this
/// still carry ITenantScopedRequest and why an authenticated pharmacy user is required.</para>
///
/// <para>Writers only. An Employee cannot add a product, so there is nothing for them to do
/// with a catalogue search, and the catalogue is the one place where a read would otherwise
/// expose 21,714 reference prices in bulk.</para>
/// </summary>
[Route("api/catalog/medicines")]
[Authorize(Policy = AuthenticationExtensions.TenantWriterPolicy)]
public class CatalogMedicinesController : ApiControllerBase
{
    /// <summary>
    /// Forgiving search: an indexed direct match first, then a bounded fuzzy fallback only if
    /// that finds almost nothing.
    ///
    /// The response carries <c>matchType</c> - "Exact" or "Suggestion" - and the UI is
    /// expected to change its heading on it. Rendering a guess as a match is how somebody
    /// imports the wrong medicine.
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(CatalogSearchResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(await Mediator.Send(
            new SearchCatalogMedicinesQuery(q ?? string.Empty, page, pageSize),
            cancellationToken));

    /// <summary>One catalogue entry, to pre-fill the import review form.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CatalogMedicineSearchItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetCatalogMedicineQuery(id), cancellationToken));
}
