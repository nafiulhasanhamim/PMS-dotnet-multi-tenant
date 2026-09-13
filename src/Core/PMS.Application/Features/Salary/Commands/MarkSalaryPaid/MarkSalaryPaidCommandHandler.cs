using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Salary;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Commands.MarkSalaryPaid;

public sealed class MarkSalaryPaidCommandHandler
    : IRequestHandler<MarkSalaryPaidCommand, Result<SalaryEntryRowDto>>
{
    private readonly IRepository<SalaryEntry, IApplicationDbContext> _entries;
    private readonly ISalaryQueries _salary;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly IDateTime _clock;
    private readonly ILogger<MarkSalaryPaidCommandHandler> _logger;

    public MarkSalaryPaidCommandHandler(
        IRepository<SalaryEntry, IApplicationDbContext> entries,
        ISalaryQueries salary,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        IDateTime clock,
        ILogger<MarkSalaryPaidCommandHandler> logger)
    {
        _entries = entries;
        _salary = salary;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<SalaryEntryRowDto>> Handle(
        MarkSalaryPaidCommand request, CancellationToken cancellationToken)
    {
        var entry = await _entries.GetByIdAsync(request.EntryId, cancellationToken);

        if (entry is null)
        {
            return Result.Failure<SalaryEntryRowDto>(
                Error.NotFound(nameof(SalaryEntry), request.EntryId));
        }

        if (entry.IsPaid)
        {
            // Not silently accepted. Paying twice is a real risk on a screen somebody refreshes,
            // and the second press would otherwise move the payment date - and with it, which
            // month the expense falls in.
            return Result.Failure<SalaryEntryRowDto>(Error.Conflict(
                $"Salary for {SalaryPeriod.Format(entry.Month, entry.Year)} was already marked "
                + $"paid on {entry.PaymentDate:dd MMM yyyy}."));
        }

        entry.MarkPaid(request.PaymentDate ?? _clock.UtcDateToday());

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Salary entry {EntryId} for {Period} marked paid on {PaymentDate}; {Net} now counts "
            + "as an operating expense in that month",
            entry.Id, SalaryPeriod.Format(entry.Month, entry.Year), entry.PaymentDate,
            entry.NetPayable);

        var row = await _salary.GetEntryAsync(entry.Id, cancellationToken);

        return row is null
            ? Result.Failure<SalaryEntryRowDto>(Error.NotFound(nameof(SalaryEntry), entry.Id))
            : Result.Success(row);
    }
}
