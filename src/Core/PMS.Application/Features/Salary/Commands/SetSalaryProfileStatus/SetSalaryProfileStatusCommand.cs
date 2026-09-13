using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Commands.SetSalaryProfileStatus;

/// <summary>
/// Takes somebody off the payroll, or puts them back on it.
///
/// <para><b>A soft delete, and it must stay one.</b> Every salary entry and every advance ever
/// recorded points at this profile. Deactivating removes them from the generation screen and the
/// advance dropdown, and leaves every month they were paid for exactly where it is.</para>
/// </summary>
public sealed record SetSalaryProfileStatusCommand(Guid ProfileId, bool IsActive)
    : IRequest<Result<SalaryProfileDetailDto>>, ITenantScopedRequest;
