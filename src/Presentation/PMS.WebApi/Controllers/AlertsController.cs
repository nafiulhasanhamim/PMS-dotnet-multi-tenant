using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Stock;
using PMS.Application.Features.Alerts.Queries.GetAlertSummary;
using PMS.Application.Features.Alerts.Queries.GetExpiredBatches;
using PMS.Application.Features.Alerts.Queries.GetExpiringBatches;
using PMS.Application.Features.Alerts.Queries.GetLowStockProducts;
using PMS.Domain.Enums;
using PMS.SharedKernel.Grid;
using PMS.WebApi.Extensions;

namespace PMS.WebApi.Controllers;

/// <summary>
/// What needs attention: stock about to expire, stock already expired, and products running out.
///
/// <para><b>Read-only, and every role can read all of it.</b> There is no create, edit or delete
/// in this module, so there is nothing to restrict — and operational visibility is worth more
/// shared than withheld. An Employee cannot remove expired stock from the shelf, but knowing not
/// to reach for it is exactly the sort of thing counter staff should be told. A module where the
/// only actions are on other screens does not need its own permissions.</para>
///
/// <para>Every action is tenant-scoped and none of them mentions a tenant. The queries carry
/// <c>ITenantScopedRequest</c>, and the global query filter supplies the WHERE — inside the
/// aggregates too, which is what makes one pharmacy's counts its own.</para>
/// </summary>
[Route("api/alerts")]
[Authorize(Policy = AuthenticationExtensions.TenantUserPolicy)]
public class AlertsController : ApiControllerBase
{
    /// <summary>
    /// The four dashboard counts and the nearest-expiry preview line.
    ///
    /// <para>Three round trips rather than five: the two batch counts share a pass, the two
    /// product counts share another, and the preview is a TOP 1. This one is called on every
    /// dashboard load and behind the sidebar badge, so it is the read in this module worth
    /// counting queries for.</para>
    ///
    /// <para>The window is not a parameter here on purpose. The cards, the badge and the default
    /// the pages open on all have to agree, and a caller who could choose a window could make
    /// them disagree.</para>
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(AlertSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetAlertSummaryQuery(), cancellationToken));

    /// <summary>
    /// Batches expiring within <paramref name="days"/>, soonest first.
    ///
    /// <para>Accepts 30, 60, 90 or 180. Anything else falls back to the configured window rather
    /// than being refused: the dropdown offers four choices, and a stale bookmark should show
    /// the default page instead of an error.</para>
    ///
    /// <para>A batch with no expiry date never appears here — see the module doc for why that is
    /// a rule rather than an accident of SQL null handling.</para>
    /// </summary>
    [HttpGet("expiring")]
    [ProducesResponseType(typeof(GridResult<ExpiringBatchDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpiring(
        [FromQuery] int? days = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AlertPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetExpiringBatchesQuery(days, page, pageSize), cancellationToken));

    /// <summary>
    /// Batches past their expiry date that still hold stock, most overdue first.
    ///
    /// <para>FEFO already refuses to sell these, so the list is not a warning about what might
    /// happen — it is a job sheet for stock that is sitting on a shelf taking up space.</para>
    /// </summary>
    [HttpGet("expired")]
    [ProducesResponseType(typeof(GridResult<ExpiringBatchDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpired(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AlertPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetExpiredBatchesQuery(page, pageSize), cancellationToken));

    /// <summary>
    /// Active products at or below their reorder level, most critically low first.
    ///
    /// <para>Expired batches do not count towards the total. A product with two hundred expired
    /// tablets and nothing else has nothing to sell, and reporting it as well stocked is how a
    /// pharmacy finds out from a customer rather than from this list.</para>
    /// </summary>
    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(GridResult<LowStockProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLowStock(
        [FromQuery] ProductType? productType = null,
        [FromQuery] LowStockStatusFilter status = LowStockStatusFilter.All,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AlertPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetLowStockProductsQuery(productType, status, page, pageSize),
            cancellationToken));
}
