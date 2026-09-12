using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Commands.UpdateSalaryProfile;

/// <summary>
/// Changes what somebody earns, or what they are called.
///
/// <para><b>A raise does not rewrite history.</b> Every generated entry copied its base salary at
/// generation time, so changing the figure here affects future generations only - see
/// <c>SalaryEntry</c>. The edit screen says so, because it is the first thing an owner wonders
/// when they type a new number in.</para>
/// </summary>
public sealed record UpdateSalaryProfileCommand(
    Guid ProfileId,
    string? Designation,
    decimal MonthlyBaseSalary,
    DateOnly JoiningDate)
    : IRequest<Result<SalaryProfileDetailDto>>, ITenantScopedRequest;
