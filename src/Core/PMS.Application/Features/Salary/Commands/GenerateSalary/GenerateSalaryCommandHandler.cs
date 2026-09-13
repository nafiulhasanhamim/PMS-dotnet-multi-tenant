using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Salary;
using PMS.Application.Features.Salary.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Commands.GenerateSalary;

/// <summary>
/// <para><b>Everything is checked before anything is written.</b> That is what makes the single
/// <c>SaveChanges</c> at the end the whole transaction, and it is not a stylistic preference:
/// <c>ExecuteInTransactionAsync</c> commits as soon as its delegate returns and inspects nothing
/// about the value, so a handler that wrote first and returned a failure afterwards would commit
/// the write. Here there is nothing to roll back, because nothing exists to roll back until every
/// check has passed. Module 4 learned this the hard way.</para>
/// </summary>
public sealed class GenerateSalaryCommandHandler
    : IRequestHandler<GenerateSalaryCommand, Result<SalaryGenerationResultDto>>
{
    private readonly IRepository<EmployeeSalaryProfile, IApplicationDbContext> _profiles;
    private readonly IRepository<SalaryAdvance, IApplicationDbContext> _advances;
    private readonly IRepository<SalaryEntry, IApplicationDbContext> _entries;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<GenerateSalaryCommandHandler> _logger;

    public GenerateSalaryCommandHandler(
        IRepository<EmployeeSalaryProfile, IApplicationDbContext> profiles,
        IRepository<SalaryAdvance, IApplicationDbContext> advances,
        IRepository<SalaryEntry, IApplicationDbContext> entries,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ICurrentUserService currentUser,
        ILogger<GenerateSalaryCommandHandler> logger)
    {
        _profiles = profiles;
        _advances = advances;
        _entries = entries;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<SalaryGenerationResultDto>> Handle(
        GenerateSalaryCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserGuid;

        if (userId is null)
        {
            return Result.Failure<SalaryGenerationResultDto>(Error.Unauthorized("Not signed in."));
        }

        var period = SalaryPeriod.Format(request.Month, request.Year);
        var requested = request.Lines.Select(l => l.ProfileId).ToList();

        // Active profiles only. Somebody deactivated between opening the screen and pressing the
        // button has left; paying them because the browser still had their row is exactly what
        // this filter prevents.
        var profiles = await _profiles.ListAsync(
            new ActiveProfilesByIdsSpec(requested), cancellationToken);

        var profileById = profiles.ToDictionary(p => p.Id);
        var missing = requested.Where(id => !profileById.ContainsKey(id)).ToList();

        if (missing.Count > 0)
        {
            return Result.Failure<SalaryGenerationResultDto>(Error.Validation(
                nameof(GenerateSalaryCommand.Lines),
                missing.Count == requested.Count
                    ? "None of the selected employees are on the active payroll."
                    : $"{missing.Count} of the selected employees are no longer on the active "
                        + "payroll. Reload the generation screen and try again."));
        }

        // The unique index refuses a duplicate at the database, which is the guarantee that
        // matters. Checking here turns it into a sentence naming the month, and - because this
        // runs before any write - leaves nothing half-created behind.
        var existing = await _entries.ListAsync(
            new EntriesForPeriodSpec(request.Month, request.Year, requested), cancellationToken);

        if (existing.Count > 0)
        {
            return Result.Failure<SalaryGenerationResultDto>(Error.Conflict(
                existing.Count == 1
                    ? $"Salary for {period} has already been generated for one of the selected "
                        + "employees."
                    : $"Salary for {period} has already been generated for {existing.Count} of "
                        + "the selected employees."));
        }

        // Tracked, because settlement writes to them. No date filter: an advance given in June
        // and never deducted must still be recoverable in October. See the specification.
        var unsettled = await _advances.ListAsync(
            new UnsettledAdvancesForProfilesSpec(requested), cancellationToken);

        var advancesByProfile = unsettled
            .GroupBy(a => a.EmployeeSalaryProfileId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<SalaryAdvance>)g.ToList());

        var created = new List<SalaryEntry>(request.Lines.Count);
        var requestedDeductions = 0m;

        foreach (var line in request.Lines)
        {
            var profile = profileById[line.ProfileId];

            var forProfile = advancesByProfile.TryGetValue(profile.Id, out var list)
                ? list
                : [];

            // The base salary is COPIED here, from the profile as it stands today. A raise next
            // year must not restate this month - see SalaryEntry.
            var entry = SalaryEntry.Generate(
                profile.Id,
                request.Month,
                request.Year,
                profile.MonthlyBaseSalary,
                line.Bonus,
                line.AdvanceDeduction,
                line.OtherDeduction,
                line.AdjustmentNotes,
                userId.Value,
                forProfile);

            created.Add(entry);
            requestedDeductions += line.AdvanceDeduction;
        }

        await _entries.AddRangeAsync(created, cancellationToken);

        // Tranches: where a month could afford only part of an advance, settlement shrank that
        // advance and produced a settled row for the part it took. Those rows are new and nothing
        // is tracking them yet, so they are added here - in the same SaveChanges, because a
        // tranche without its entry would be money recovered against nothing.
        var tranches = created.SelectMany(e => e.NewAdvanceTranches).ToList();

        if (tranches.Count > 0)
        {
            await _advances.AddRangeAsync(tranches, cancellationToken);
        }

        // One SaveChanges, so the entries, the tranches and the advance settlements commit
        // together or not at all. EF wraps it in a transaction; nothing else here needs one.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var settled = created.Sum(e => e.AdvanceDeduction);

        // What is genuinely left owing, read from the advances themselves rather than from the
        // arithmetic. A split advance has already had its Amount reduced to the remainder, so
        // summing the still-unsettled rows is exactly what carries into next month.
        var carriedOver = unsettled.Where(a => !a.IsSettled).Sum(a => a.Amount);

        _logger.LogInformation(
            "Salary generated for {Count} employees for {Period}: net {Net}, advances settled "
            + "{Settled} of {Requested} requested, {CarriedOver} carried over",
            created.Count, period, created.Sum(e => e.NetPayable), settled, requestedDeductions,
            carriedOver);

        return new SalaryGenerationResultDto(
            request.Month,
            request.Year,
            created.Count,
            created.Sum(e => e.NetPayable),
            settled,
            carriedOver);
    }
}
