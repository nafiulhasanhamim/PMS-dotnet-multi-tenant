using PMS.Application.Common.DTOs;
using PMS.SharedKernel.Grid;

namespace PMS.Application.Interfaces;

/// <summary>
/// Reads over salary profiles, advances and generated entries.
///
/// <para><b>Nothing here projects an advance total as a correlated subquery inside a row.</b>
/// "Each profile with the sum of its unsettled advances" is exactly the shape SQL Server has
/// rejected three times in this codebase — <em>"Cannot perform an aggregate function on an
/// expression containing an aggregate or a subquery"</em>, hit in Modules 6, 7 and 8. Every method
/// below that needs one pages or filters its profiles first, then sums advances over the
/// <c>SalaryAdvances</c> table filtered by those ids, and joins the two in memory. The same
/// structure the supplier balances use, for the same reason.</para>
///
/// <para>No method takes a tenant id. Every entity read implements <c>ITenantEntity</c>, so the
/// global query filter supplies the pharmacy — inside the aggregates too.</para>
/// </summary>
public interface ISalaryQueries
{
    // ── Profiles ─────────────────────────────────────────────────────────────────────────

    /// <summary>One page of salary profiles, each with what the employee still owes back.</summary>
    /// <param name="search">Matches the employee's name or designation.</param>
    Task<GridResult<SalaryProfileListItemDto>> GetProfilesAsync(
        string? search,
        SalaryProfileStatusFilter status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>One profile, or null if this pharmacy has no such profile.</summary>
    Task<SalaryProfileDetailDto?> GetProfileAsync(
        Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Users of this pharmacy who do not already have an active profile — the "add profile"
    /// dropdown.
    ///
    /// <para>Excluding the ones already on the payroll is not cosmetic: the filtered unique index
    /// would refuse them, and a dropdown whose options can fail teaches people to distrust it.</para>
    /// </summary>
    Task<IReadOnlyList<SalaryEligibleUserDto>> GetEligibleUsersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Active profiles for the advance form's employee picker, each with its outstanding total.
    ///
    /// <para>The total travels with the option so the screen can show "৳2,000 already
    /// outstanding" the moment somebody is chosen, without another round-trip — which is the
    /// whole point of showing it. Somebody about to hand over more cash should see what is
    /// already owed.</para>
    /// </summary>
    Task<IReadOnlyList<SalaryProfileOptionDto>> GetProfileOptionsAsync(
        CancellationToken cancellationToken = default);

    // ── Advances ─────────────────────────────────────────────────────────────────────────

    /// <summary>One page of advances, newest first, settled and unsettled together.</summary>
    Task<GridResult<SalaryAdvanceRowDto>> GetAdvancesAsync(
        Guid? profileId,
        AdvanceSettlementFilter settlement,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    // ── Generation ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every active profile for a month, with its unsettled advances broken out.
    ///
    /// <para>Profiles that already have an entry for the period come back flagged rather than
    /// missing — see <see cref="SalaryGenerationRowDto"/>.</para>
    /// </summary>
    Task<SalaryGenerationPreviewDto> GetGenerationPreviewAsync(
        int month, int year, CancellationToken cancellationToken = default);

    // ── Entries ──────────────────────────────────────────────────────────────────────────

    /// <summary>One page of generated salaries. Newest period first, then by employee name.</summary>
    Task<GridResult<SalaryEntryRowDto>> GetEntriesAsync(
        int? month,
        int? year,
        Guid? profileId,
        SalaryStatusFilter status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>One entry, or null if this pharmacy has no such entry.</summary>
    Task<SalaryEntryRowDto?> GetEntryAsync(
        Guid entryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Everything the printable slip needs, including which advances this entry recovered.
    ///
    /// <para>The advance breakdown is the reason a slip is worth printing at all: an employee
    /// handed ৳13,000 against a ৳15,000 salary can see the ৳2,000 they already had, and on what
    /// date.</para>
    /// </summary>
    Task<SalarySlipDto?> GetSlipAsync(Guid entryId, CancellationToken cancellationToken = default);

    // ── Landing ──────────────────────────────────────────────────────────────────────────

    /// <summary>The hub's two summary figures, plus the active headcount.</summary>
    Task<SalarySummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}
