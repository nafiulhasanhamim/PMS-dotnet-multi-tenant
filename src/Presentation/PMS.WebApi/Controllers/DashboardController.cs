using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.DTOs;
using PMS.Application.Features.Dashboard.Queries.GetDashboard;
using PMS.WebApi.Extensions;

namespace PMS.WebApi.Controllers;

/// <summary>
/// The home screen.
///
/// <para><b>Every role, with tiered content.</b> An Employee sees what needs acting on — what is
/// expiring, what has expired, what is low — because those are things a person at a counter can
/// do something about. A Pharmacist additionally sees the day's trading and the month's
/// antibiotics. An Admin additionally sees profit, supplier dues and unpaid salary.</para>
///
/// <para><b>The tiering is done on the server.</b> A figure a role may not see is absent from the
/// response rather than hidden by the page — CSS is not an access control.</para>
/// </summary>
[Route("api/dashboard")]
[Authorize(Policy = AuthenticationExtensions.TenantUserPolicy)]
public class DashboardController : ApiControllerBase
{
    /// <summary>
    /// Everything the home screen shows, in <b>one</b> request.
    ///
    /// <para>Deliberately one call rather than one per card. This loads on every visit and for
    /// every user; several round trips to draw one screen means several chances at a partial
    /// render and several times the latency on a shop's connection.</para>
    ///
    /// <para>Each figure comes from the module that owns it — alerts from Module 6, trading and
    /// supplier dues from Module 8, antibiotics from Module 7, salary from Module 9 — so every
    /// card equals the screen it links to by construction rather than by luck.</para>
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetDashboardQuery(), cancellationToken));
}
