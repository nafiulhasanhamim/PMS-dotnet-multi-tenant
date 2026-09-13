using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Commands.RecordSalaryAdvance;

public sealed class RecordSalaryAdvanceCommandHandler
    : IRequestHandler<RecordSalaryAdvanceCommand, Result<SalaryAdvanceRecordedDto>>
{
    private readonly IRepository<EmployeeSalaryProfile, IApplicationDbContext> _profiles;
    private readonly IRepository<SalaryAdvance, IApplicationDbContext> _advances;
    private readonly ISalaryQueries _salary;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly ILogger<RecordSalaryAdvanceCommandHandler> _logger;

    public RecordSalaryAdvanceCommandHandler(
        IRepository<EmployeeSalaryProfile, IApplicationDbContext> profiles,
        IRepository<SalaryAdvance, IApplicationDbContext> advances,
        ISalaryQueries salary,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ICurrentUserService currentUser,
        IDateTime clock,
        ILogger<RecordSalaryAdvanceCommandHandler> logger)
    {
        _profiles = profiles;
        _advances = advances;
        _salary = salary;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<SalaryAdvanceRecordedDto>> Handle(
        RecordSalaryAdvanceCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserGuid;

        if (userId is null)
        {
            return Result.Failure<SalaryAdvanceRecordedDto>(Error.Unauthorized("Not signed in."));
        }

        var profile = await _profiles.GetByIdAsync(request.ProfileId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure<SalaryAdvanceRecordedDto>(
                Error.NotFound(nameof(EmployeeSalaryProfile), request.ProfileId));
        }

        // Active only. Handing an advance to somebody who has left the pharmacy is either a
        // mistake or a payment of a different kind, and recording it here would put a row on a
        // generation screen that will never show this employee again.
        if (!profile.IsActive)
        {
            return Result.Failure<SalaryAdvanceRecordedDto>(Error.Validation(
                nameof(RecordSalaryAdvanceCommand.ProfileId),
                "That employee is no longer on the payroll, so an advance recorded against them "
                + "could never be deducted. Reactivate their profile first."));
        }

        var advance = new SalaryAdvance(
            profile.Id,
            request.Amount,
            request.AdvanceDate ?? _clock.UtcDateToday(),
            request.Reason,
            userId.Value);

        await _advances.AddAsync(advance, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Read back through the same service every screen uses, so the figure the form shows next
        // is the figure the generation screen will show.
        var options = await _salary.GetProfileOptionsAsync(cancellationToken);
        var option = options.FirstOrDefault(o => o.ProfileId == profile.Id);

        _logger.LogInformation(
            "Advance {AdvanceId} of {Amount} recorded for profile {ProfileId} on {Date}; "
            + "outstanding is now {Outstanding}",
            advance.Id, advance.Amount, profile.Id, advance.AdvanceDate,
            option?.UnsettledAdvanceTotal ?? advance.Amount);

        return new SalaryAdvanceRecordedDto(
            advance.Id,
            profile.Id,
            option?.EmployeeName ?? string.Empty,
            advance.Amount,
            advance.AdvanceDate,
            option?.UnsettledAdvanceTotal ?? advance.Amount);
    }
}
