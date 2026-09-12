using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Salary;
using PMS.Application.Features.Salary.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Commands.UpdateSalaryEntry;

public sealed class UpdateSalaryEntryCommandHandler
    : IRequestHandler<UpdateSalaryEntryCommand, Result<SalaryEntryRowDto>>
{
    private readonly IRepository<SalaryEntry, IApplicationDbContext> _entries;
    private readonly IRepository<SalaryAdvance, IApplicationDbContext> _advances;
    private readonly ISalaryQueries _salary;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ILogger<UpdateSalaryEntryCommandHandler> _logger;

    public UpdateSalaryEntryCommandHandler(
        IRepository<SalaryEntry, IApplicationDbContext> entries,
        IRepository<SalaryAdvance, IApplicationDbContext> advances,
        ISalaryQueries salary,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ILogger<UpdateSalaryEntryCommandHandler> logger)
    {
        _entries = entries;
        _advances = advances;
        _salary = salary;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<SalaryEntryRowDto>> Handle(
        UpdateSalaryEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _entries.GetByIdAsync(request.EntryId, cancellationToken);

        if (entry is null)
        {
            return Result.Failure<SalaryEntryRowDto>(
                Error.NotFound(nameof(SalaryEntry), request.EntryId));
        }

        // The lock. The UI hides the edit action on a paid entry; this is what makes hiding it
        // more than a suggestion.
        if (entry.IsPaid)
        {
            return Result.Failure<SalaryEntryRowDto>(Error.Conflict(
                $"Salary for {SalaryPeriod.Format(entry.Month, entry.Year)} was paid on "
                + $"{entry.PaymentDate:dd MMM yyyy} and cannot be changed."));
        }

        // Both halves: what this entry currently holds, and anything still unsettled. Settlement
        // can move either way when the deduction is edited - see the specification.
        var candidates = await _advances.ListAsync(
            new AdvancesForEntryRevisionSpec(entry.EmployeeSalaryProfileId, entry.Id),
            cancellationToken);

        var before = entry.NetPayable;

        entry.Revise(
            request.Bonus,
            request.AdvanceDeduction,
            request.OtherDeduction,
            request.AdjustmentNotes,
            candidates);

        // Raising the deduction can leave this entry able to afford only part of the next
        // advance, which splits it. Those new rows are untracked - see the generation handler.
        if (entry.NewAdvanceTranches.Count > 0)
        {
            await _advances.AddRangeAsync(entry.NewAdvanceTranches, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Salary entry {EntryId} revised; net payable {Before} -> {After}, "
            + "advances settled {Settled}",
            entry.Id, before, entry.NetPayable, entry.AdvanceDeduction);

        var row = await _salary.GetEntryAsync(entry.Id, cancellationToken);

        return row is null
            ? Result.Failure<SalaryEntryRowDto>(Error.NotFound(nameof(SalaryEntry), entry.Id))
            : Result.Success(row);
    }
}
