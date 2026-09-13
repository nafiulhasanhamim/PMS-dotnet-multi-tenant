using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Commands.CreateSalaryProfile;

/// <summary>
/// Puts somebody on the payroll.
///
/// <para>Admin only, like everything in this module. What people earn is the one category of data
/// a pharmacy owner would not show their own counter staff, and the module is gated as a whole
/// rather than screen by screen for that reason.</para>
/// </summary>
public sealed record CreateSalaryProfileCommand(
    Guid UserId,
    string? Designation,
    decimal MonthlyBaseSalary,
    DateOnly JoiningDate)
    : IRequest<Result<SalaryProfileDetailDto>>, ITenantScopedRequest;
