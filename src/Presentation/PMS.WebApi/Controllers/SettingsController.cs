using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.DTOs;
using PMS.Application.Features.Settings.Commands.SetAntibioticMode;
using PMS.Application.Features.Settings.Commands.UpdateSettings;
using PMS.Application.Features.Settings.Queries.GetAntibioticMode;
using PMS.Application.Features.Settings.Queries.GetSettings;
using PMS.Domain.Enums;
using PMS.WebApi.Extensions;

namespace PMS.WebApi.Controllers;

/// <summary>
/// Per-pharmacy settings.
///
/// <para><b>Read by anyone, changed by an Admin</b>, and the split is not arbitrary. The billing
/// screen has to know the discount caps and the antibiotic mode on every load; an invoice and a
/// salary slip need the pharmacy's name, address and phone. So an Employee at a till reads these
/// several times a day without ever being able to change one. Changing them alters what staff may
/// sell, what they may discount and what the pharmacy records against the law — an owner's
/// decisions.</para>
///
/// <para><b>Module 10 added the whole-set pair below and kept the antibiotic-mode pair.</b> Both
/// write through the same <c>ISettingsService</c> into the same table, so there is one source of
/// truth however a value is changed. The single-setting endpoints survive because Module 7's own
/// screen calls them, and because "change one setting" is a smaller thing to ask for than "save
/// the whole page".</para>
/// </summary>
[Route("api/settings")]
[Authorize(Policy = AuthenticationExtensions.TenantUserPolicy)]
public class SettingsController : ApiControllerBase
{
    /// <summary>
    /// Every setting for this pharmacy, typed.
    ///
    /// <para>Any tenant user. See the class remarks: a cashier's till reads the discount caps and
    /// the antibiotic mode, and an invoice reads the pharmacy's details.</para>
    ///
    /// <para>A missing key falls back to the value it had as a hardcoded constant before Module
    /// 10, so this never fails because a seed was incomplete — it logs a warning instead.</para>
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(SettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetSettingsQuery(), cancellationToken));

    /// <summary>
    /// Changes some or all of them. <b>Admin only.</b>
    ///
    /// <para><b>Every field is optional, and omitting one leaves it alone.</b> The settings page
    /// posts the whole form; a caller changing one value sends one field. An empty string is a
    /// value being set rather than an omission, which is what makes "clear the pharmacy name"
    /// expressible — and refused, because it appears on every invoice.</para>
    ///
    /// <para><b>Nothing is applied unless everything validates.</b> A body carrying a good
    /// pharmacy name and a discount cap of 150 changes neither.</para>
    ///
    /// <para>Takes effect on the next request. Settings are read once per request and cached only
    /// for its duration, so there is no window in which a cashier is still being refused under the
    /// old cap.</para>
    /// </summary>
    [HttpPut]
    [Authorize(Policy = AuthenticationExtensions.TenantAdminPolicy)]
    [ProducesResponseType(typeof(SettingsSavedDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] UpdateSettingsCommand command,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(command, cancellationToken));

    /// <summary>
    /// How strictly this pharmacy captures antibiotic prescriptions.
    ///
    /// <para>Any tenant user. See the class remarks for why this is not Admin-only, and why it
    /// survives alongside <c>GET /api/settings</c>.</para>
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
