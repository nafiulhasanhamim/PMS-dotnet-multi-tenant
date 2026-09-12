using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Commands.CreateSalaryProfile;

public sealed class CreateSalaryProfileCommandHandler
    : IRequestHandler<CreateSalaryProfileCommand, Result<SalaryProfileDetailDto>>
{
    private readonly IRepository<EmployeeSalaryProfile, IApplicationDbContext> _profiles;
    private readonly ISalaryQueries _salary;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ILogger<CreateSalaryProfileCommandHandler> _logger;

    public CreateSalaryProfileCommandHandler(
        IRepository<EmployeeSalaryProfile, IApplicationDbContext> profiles,
        ISalaryQueries salary,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ILogger<CreateSalaryProfileCommandHandler> logger)
    {
        _profiles = profiles;
        _salary = salary;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<SalaryProfileDetailDto>> Handle(
        CreateSalaryProfileCommand request, CancellationToken cancellationToken)
    {
        // The SAME list the dropdown is built from, checked again here.
        //
        // It answers two questions at once, and both matter. Users is a global table with no
        // tenant filter, so "is this person one of ours" cannot be taken on trust from a posted
        // id - without this check an Admin could put another pharmacy's staff on their payroll by
        // editing one field. And "are they already on the payroll" is refused by the filtered
        // unique index anyway; catching it here turns a database error into a sentence.
        //
        // Sharing the query with the dropdown is deliberate: a screen that offers options the
        // command rejects is worse than no screen.
        var eligible = await _salary.GetEligibleUsersAsync(cancellationToken);
        var chosen = eligible.FirstOrDefault(u => u.UserId == request.UserId);

        if (chosen is null)
        {
            return Result.Failure<SalaryProfileDetailDto>(Error.Validation(
                nameof(CreateSalaryProfileCommand.UserId),
                "That employee is not available - they may already have a salary profile, or "
                + "they are not a member of this pharmacy."));
        }

        var profile = new EmployeeSalaryProfile(
            request.UserId, request.Designation, request.MonthlyBaseSalary, request.JoiningDate);

        await _profiles.AddAsync(profile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Salary profile {ProfileId} created for '{Employee}' ({UserId}) at {Salary} a month",
            profile.Id, chosen.Name, chosen.UserId, profile.MonthlyBaseSalary);

        return new SalaryProfileDetailDto(
            profile.Id, profile.UserId, chosen.Name, chosen.Email, profile.Designation,
            profile.MonthlyBaseSalary, profile.JoiningDate, profile.IsActive);
    }
}
