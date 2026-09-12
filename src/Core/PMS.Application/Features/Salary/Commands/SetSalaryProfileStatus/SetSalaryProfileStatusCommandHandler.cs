using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.DTOs;
using PMS.Application.Features.Salary.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Commands.SetSalaryProfileStatus;

public sealed class SetSalaryProfileStatusCommandHandler
    : IRequestHandler<SetSalaryProfileStatusCommand, Result<SalaryProfileDetailDto>>
{
    private readonly IRepository<EmployeeSalaryProfile, IApplicationDbContext> _profiles;
    private readonly ISalaryQueries _salary;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ILogger<SetSalaryProfileStatusCommandHandler> _logger;

    public SetSalaryProfileStatusCommandHandler(
        IRepository<EmployeeSalaryProfile, IApplicationDbContext> profiles,
        ISalaryQueries salary,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ILogger<SetSalaryProfileStatusCommandHandler> logger)
    {
        _profiles = profiles;
        _salary = salary;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<SalaryProfileDetailDto>> Handle(
        SetSalaryProfileStatusCommand request, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByIdAsync(request.ProfileId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure<SalaryProfileDetailDto>(
                Error.NotFound(nameof(EmployeeSalaryProfile), request.ProfileId));
        }

        if (request.IsActive && !profile.IsActive)
        {
            // A rejoiner may already have been given a NEW profile since this one was
            // deactivated. Reactivating the old one would then put the same person on the payroll
            // twice, which the filtered unique index refuses - as a 500 rather than a sentence,
            // unless it is caught here.
            var existing = await _profiles.FirstOrDefaultAsync(
                new ActiveProfileForUserSpec(profile.UserId), cancellationToken);

            if (existing is not null)
            {
                return Result.Failure<SalaryProfileDetailDto>(Error.Conflict(
                    "This employee already has an active salary profile. Deactivate that one "
                    + "first if you meant to bring this older profile back."));
            }
        }

        if (request.IsActive)
        {
            profile.Reactivate();
        }
        else
        {
            profile.Deactivate();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Salary profile {ProfileId} {Action}; salary history is unaffected",
            profile.Id, request.IsActive ? "reactivated" : "deactivated");

        var detail = await _salary.GetProfileAsync(profile.Id, cancellationToken);

        return detail is null
            ? Result.Failure<SalaryProfileDetailDto>(
                Error.NotFound(nameof(EmployeeSalaryProfile), profile.Id))
            : Result.Success(detail);
    }
}
