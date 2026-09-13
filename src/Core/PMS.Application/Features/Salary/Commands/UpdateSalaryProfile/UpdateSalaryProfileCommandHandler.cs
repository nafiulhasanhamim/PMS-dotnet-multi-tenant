using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Commands.UpdateSalaryProfile;

public sealed class UpdateSalaryProfileCommandHandler
    : IRequestHandler<UpdateSalaryProfileCommand, Result<SalaryProfileDetailDto>>
{
    private readonly IRepository<EmployeeSalaryProfile, IApplicationDbContext> _profiles;
    private readonly ISalaryQueries _salary;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ILogger<UpdateSalaryProfileCommandHandler> _logger;

    public UpdateSalaryProfileCommandHandler(
        IRepository<EmployeeSalaryProfile, IApplicationDbContext> profiles,
        ISalaryQueries salary,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ILogger<UpdateSalaryProfileCommandHandler> logger)
    {
        _profiles = profiles;
        _salary = salary;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<SalaryProfileDetailDto>> Handle(
        UpdateSalaryProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByIdAsync(request.ProfileId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure<SalaryProfileDetailDto>(
                Error.NotFound(nameof(EmployeeSalaryProfile), request.ProfileId));
        }

        var previousSalary = profile.MonthlyBaseSalary;

        // An INACTIVE profile can still be edited - correcting a designation or a mistyped
        // joining date on somebody who has left is legitimate, and nothing about it can reach a
        // generated entry.
        profile.Update(request.Designation, request.MonthlyBaseSalary, request.JoiningDate);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Salary profile {ProfileId} updated; base salary {Before} -> {After}. "
            + "Entries already generated are unchanged.",
            profile.Id, previousSalary, profile.MonthlyBaseSalary);

        var detail = await _salary.GetProfileAsync(profile.Id, cancellationToken);

        return detail is null
            ? Result.Failure<SalaryProfileDetailDto>(
                Error.NotFound(nameof(EmployeeSalaryProfile), profile.Id))
            : Result.Success(detail);
    }
}
