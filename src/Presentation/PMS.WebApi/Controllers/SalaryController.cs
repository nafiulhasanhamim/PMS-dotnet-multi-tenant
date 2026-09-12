using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Salary;
using PMS.Application.Features.Salary.Commands.CreateSalaryProfile;
using PMS.Application.Features.Salary.Commands.GenerateSalary;
using PMS.Application.Features.Salary.Commands.MarkSalaryPaid;
using PMS.Application.Features.Salary.Commands.RecordSalaryAdvance;
using PMS.Application.Features.Salary.Commands.SetSalaryProfileStatus;
using PMS.Application.Features.Salary.Commands.UpdateSalaryEntry;
using PMS.Application.Features.Salary.Commands.UpdateSalaryProfile;
using PMS.Application.Features.Salary.Queries.GetEligibleUsers;
using PMS.Application.Features.Salary.Queries.GetGenerationPreview;
using PMS.Application.Features.Salary.Queries.GetSalaryAdvances;
using PMS.Application.Features.Salary.Queries.GetSalaryEntries;
using PMS.Application.Features.Salary.Queries.GetSalaryEntry;
using PMS.Application.Features.Salary.Queries.GetSalaryProfile;
using PMS.Application.Features.Salary.Queries.GetSalaryProfileOptions;
using PMS.Application.Features.Salary.Queries.GetSalaryProfiles;
using PMS.Application.Features.Salary.Queries.GetSalarySlip;
using PMS.Application.Features.Salary.Queries.GetSalarySummary;
using PMS.SharedKernel.Grid;
using PMS.WebApi.Extensions;

namespace PMS.WebApi.Controllers;

/// <summary>
/// Who is on the payroll, what they are paid, and what they have already been advanced.
///
/// <para><b>Admin only, in full, with no read-only variant.</b> Not one action here is available
/// to a Pharmacist or an Employee - not even listing profiles. What colleagues earn is the single
/// most sensitive thing a small pharmacy stores, and a counter assistant who can see the payroll
/// can see what everybody standing next to them is paid. The policy is declared once on the
/// controller rather than per action so that a new endpoint cannot be added without it, which is
/// exactly how a module-wide rule leaks.</para>
///
/// <para>Module 8's Reports controller is gated the same way, for the same reason.</para>
///
/// <para>No action takes a tenant id. Every query and command carries
/// <c>ITenantScopedRequest</c> and the global query filter supplies the pharmacy - which is what
/// makes one pharmacy's payroll invisible to another, even by direct id.</para>
/// </summary>
[Route("api/salary")]
[Authorize(Policy = AuthenticationExtensions.TenantAdminPolicy)]
public class SalaryController : ApiControllerBase
{
    // ── Profiles ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One page of the payroll, each row with what the employee still owes back in advances.
    /// </summary>
    /// <param name="search">Matches employee name or designation.</param>
    [HttpGet("profiles")]
    [ProducesResponseType(typeof(GridResult<SalaryProfileListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfiles(
        [FromQuery] string? search = null,
        [FromQuery] SalaryProfileStatusFilter status = SalaryProfileStatusFilter.Active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = SalaryPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetSalaryProfilesQuery(search, status, page, pageSize), cancellationToken));

    /// <summary>One profile.</summary>
    [HttpGet("profiles/{id:guid}")]
    [ProducesResponseType(typeof(SalaryProfileDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(
        Guid id, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetSalaryProfileQuery(id), cancellationToken));

    /// <summary>
    /// Users of this pharmacy who are not already on the payroll - the "add profile" dropdown.
    ///
    /// <para>The create endpoint checks this same list rather than trusting the posted id, so the
    /// dropdown can never offer an option the command rejects.</para>
    /// </summary>
    [HttpGet("profiles/eligible-users")]
    [ProducesResponseType(typeof(IReadOnlyList<SalaryEligibleUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleUsers(
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetEligibleUsersQuery(), cancellationToken));

    /// <summary>Active profiles for the advance form, each with its outstanding total.</summary>
    [HttpGet("profiles/options")]
    [ProducesResponseType(typeof(IReadOnlyList<SalaryProfileOptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfileOptions(
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetSalaryProfileOptionsQuery(), cancellationToken));

    /// <summary>Puts somebody on the payroll.</summary>
    [HttpPost("profiles")]
    [ProducesResponseType(typeof(SalaryProfileDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProfile(
        [FromBody] CreateSalaryProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetProfile), new { id = result.Value!.Id }, result.Value)
            : HandleResult(result);
    }

    /// <summary>
    /// Changes what somebody earns, or what they are called.
    ///
    /// <para><b>Retroactively changes nothing.</b> Every generated entry copied its base salary at
    /// generation time, so a raise applies to future generations only.</para>
    /// </summary>
    [HttpPut("profiles/{id:guid}")]
    [ProducesResponseType(typeof(SalaryProfileDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(
        Guid id,
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new UpdateSalaryProfileCommand(
                id, request.Designation, request.MonthlyBaseSalary, request.JoiningDate),
            cancellationToken));

    /// <summary>
    /// Takes somebody off the payroll. A soft delete.
    ///
    /// <para>Removes them from the generation screen and the advance dropdown. Removes nothing
    /// that was already paid - every entry and every advance stays exactly where it was, because
    /// deleting the record of what somebody was paid is not a thing this system does.</para>
    /// </summary>
    [HttpPatch("profiles/{id:guid}/deactivate")]
    [ProducesResponseType(typeof(SalaryProfileDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeactivateProfile(
        Guid id, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new SetSalaryProfileStatusCommand(id, IsActive: false), cancellationToken));

    /// <summary>Puts somebody back on the payroll.</summary>
    [HttpPatch("profiles/{id:guid}/reactivate")]
    [ProducesResponseType(typeof(SalaryProfileDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReactivateProfile(
        Guid id, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new SetSalaryProfileStatusCommand(id, IsActive: true), cancellationToken));

    // ── Advances ─────────────────────────────────────────────────────────────────────────

    /// <summary>One page of advances, settled and unsettled together, newest first.</summary>
    [HttpGet("advances")]
    [ProducesResponseType(typeof(GridResult<SalaryAdvanceRowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdvances(
        [FromQuery] Guid? profileId = null,
        [FromQuery] AdvanceSettlementFilter settlement = AdvanceSettlementFilter.All,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = SalaryPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetSalaryAdvancesQuery(profileId, settlement, page, pageSize),
            cancellationToken));

    /// <summary>
    /// Records cash handed to an employee mid-month.
    ///
    /// <para><b>Recorded when it is given.</b> An advance remembered at month-end is an advance
    /// that gets forgotten, and a forgotten advance is paid twice. It also counts as an operating
    /// expense on <c>advanceDate</c>, because that is the day the cash left the register - see
    /// the module doc's worked example.</para>
    /// </summary>
    [HttpPost("advances")]
    [ProducesResponseType(typeof(SalaryAdvanceRecordedDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordAdvance(
        [FromBody] RecordSalaryAdvanceCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? Created($"/api/salary/advances/{result.Value!.Id}", result.Value)
            : HandleResult(result);
    }

    // ── Generation ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every active profile for a month, with its unsettled advances broken out per advance.
    ///
    /// <para>Profiles already generated for the period come back with
    /// <c>alreadyGenerated: true</c> rather than being omitted - an employee missing from the list
    /// is indistinguishable from one nobody put on the payroll.</para>
    /// </summary>
    [HttpGet("generate/preview")]
    [ProducesResponseType(typeof(SalaryGenerationPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetGenerationPreview(
        [FromQuery] int month,
        [FromQuery] int year,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetGenerationPreviewQuery(month, year), cancellationToken));

    /// <summary>
    /// Creates a month's salary entries and settles the advances they cover.
    ///
    /// <para><b>One transaction.</b> Every entry and every advance settlement either happens or
    /// none of it does.</para>
    ///
    /// <para>Each line's <c>advanceDeduction</c> is a request, not a result: it is capped so net
    /// payable cannot go below zero, and the advances it covers are settled oldest-first and only
    /// ever whole. The response says what was actually settled and what carried over.</para>
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(SalaryGenerationResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Generate(
        [FromBody] GenerateSalaryCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? Created(
                $"/api/salary/entries?month={result.Value!.Month}&year={result.Value.Year}",
                result.Value)
            : HandleResult(result);
    }

    // ── Entries ──────────────────────────────────────────────────────────────────────────

    /// <summary>One page of generated salaries. Newest period first, then by employee name.</summary>
    [HttpGet("entries")]
    [ProducesResponseType(typeof(GridResult<SalaryEntryRowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEntries(
        [FromQuery] int? month = null,
        [FromQuery] int? year = null,
        [FromQuery] Guid? profileId = null,
        [FromQuery] SalaryStatusFilter status = SalaryStatusFilter.All,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = SalaryPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetSalaryEntriesQuery(month, year, profileId, status, page, pageSize),
            cancellationToken));

    /// <summary>One entry.</summary>
    [HttpGet("entries/{id:guid}")]
    [ProducesResponseType(typeof(SalaryEntryRowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEntry(
        Guid id, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetSalaryEntryQuery(id), cancellationToken));

    /// <summary>
    /// Corrects a salary that has not been paid yet.
    ///
    /// <para><b>409 once the entry is paid</b>, and that is the point rather than an edge case.
    /// The money has gone and the employee has a slip; an entry that changes afterwards is not a
    /// record of anything. The UI hides the edit action on a paid row, and this is what makes
    /// hiding it more than a suggestion.</para>
    ///
    /// <para>Re-running an edit re-runs advance settlement, so the advances marked against this
    /// entry always sum to its <c>advanceDeduction</c> - lowering the deduction genuinely
    /// releases advances back into next month.</para>
    /// </summary>
    [HttpPut("entries/{id:guid}")]
    [ProducesResponseType(typeof(SalaryEntryRowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateEntry(
        Guid id,
        [FromBody] UpdateEntryRequest request,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new UpdateSalaryEntryCommand(
                id, request.Bonus, request.AdvanceDeduction, request.OtherDeduction,
                request.AdjustmentNotes),
            cancellationToken));

    /// <summary>
    /// Records that a salary has been handed over.
    ///
    /// <para>Two things happen: the entry locks permanently, and it becomes an operating expense
    /// <b>on the payment date</b>. An August salary paid on 2 September lands in September's
    /// profit and loss. That is cash-basis accounting and it is deliberate.</para>
    /// </summary>
    [HttpPatch("entries/{id:guid}/pay")]
    [ProducesResponseType(typeof(SalaryEntryRowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkPaid(
        Guid id,
        [FromBody] MarkPaidRequest? request,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new MarkSalaryPaidCommand(id, request?.PaymentDate), cancellationToken));

    /// <summary>
    /// The printable slip, including which advances this entry recovered and on what dates.
    ///
    /// <para>Available on an unpaid entry too: handing somebody the breakdown before the money
    /// moves is how a disagreement gets settled before it becomes one.</para>
    /// </summary>
    [HttpGet("entries/{id:guid}/slip")]
    [ProducesResponseType(typeof(SalarySlipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSlip(
        Guid id, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetSalarySlipQuery(id), cancellationToken));

    // ── Landing ──────────────────────────────────────────────────────────────────────────

    /// <summary>What is owed to staff, and what staff owe back.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(SalarySummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetSalarySummaryQuery(), cancellationToken));

    /// <summary>The body of a profile update; the id comes from the route.</summary>
    public sealed record UpdateProfileRequest(
        string? Designation,
        decimal MonthlyBaseSalary,
        DateOnly JoiningDate);

    /// <summary>The body of an entry edit; the id comes from the route.</summary>
    public sealed record UpdateEntryRequest(
        decimal Bonus,
        decimal AdvanceDeduction,
        decimal OtherDeduction,
        string? AdjustmentNotes);

    /// <summary>The body of a payment. Omit it entirely, or omit the date, to mean today.</summary>
    public sealed record MarkPaidRequest(DateOnly? PaymentDate);
}
