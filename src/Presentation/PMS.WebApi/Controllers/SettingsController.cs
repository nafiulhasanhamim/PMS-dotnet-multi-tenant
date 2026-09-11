using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.DTOs;
using PMS.Application.Features.Settings.Commands.SetAntibioticMode;
using PMS.Application.Features.Settings.Queries.GetAntibioticMode;
using PMS.Domain.Enums;
using PMS.WebApi.Extensions;

namespace PMS.WebApi.Controllers;

/// <summary>
/// Per-pharmacy settings. One today.
///
/// <para><b>Read by anyone, changed by an Admin</b>, and the split is not arbitrary. The billing
/// screen has to know the antibiotic mode on every load to decide whether to draw the
/// prescription panel, and the register has to know it to decide whether a blank prescription
/// column is a finding or an expectation — so an Employee at a till reads it several times a day.
/// Changing it alters what staff may sell and what the pharmacy records against the law, which is
/// an owner's decision.</para>
/// </summary>
[Route("api/settings")]
[Authorize(Policy = AuthenticationExtensions.TenantUserPolicy)]
public class SettingsController : ApiControllerBase
{
    /// <summary>
    /// How strictly this pharmacy captures antibiotic prescriptions.
    ///
    /// <para>Any tenant user. See the class remarks for why this is not Admin-only.</para>
    /// </summary>
    [HttpGet("antibiotic-mode")]
    [ProducesResponseType(typeof(AntibioticModeDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAntibioticMode(
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetAntibioticModeQuery(), cancellationToken));

    /// <summary>
    /// Changes the mode. <b>Admin only.</b>
    ///
    /// <para>Takes effect on the next sale — the setting is read once per request, so there is no
    /// cache to wait out and no redeploy. Past sales are not reinterpreted: a sale made under Off
    /// was correct under the rules in force when it happened.</para>
    /// </summary>
    [HttpPut("antibiotic-mode")]
    [Authorize(Policy = AuthenticationExtensions.TenantAdminPolicy)]
    [ProducesResponseType(typeof(AntibioticModeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetAntibioticMode(
        [FromBody] SetAntibioticModeRequest request,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new SetAntibioticModeCommand(request.Mode), cancellationToken));
}

/// <param name="Mode">Off, Optional or Required. See the enum for what each one does.</param>
public sealed record SetAntibioticModeRequest(AntibioticPrescriptionMode Mode);
