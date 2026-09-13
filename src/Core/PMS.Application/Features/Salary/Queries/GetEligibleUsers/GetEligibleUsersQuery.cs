using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Queries.GetEligibleUsers;

/// <summary>
/// Users of this pharmacy who are not already on the payroll - the "add profile" dropdown.
///
/// <para>The create handler checks the same list rather than trusting the posted id, so a screen
/// can never offer an option the command would reject.</para>
/// </summary>
public sealed record GetEligibleUsersQuery
    : IRequest<Result<IReadOnlyList<SalaryEligibleUserDto>>>, ITenantScopedRequest;

public sealed class GetEligibleUsersQueryHandler
    : IRequestHandler<GetEligibleUsersQuery, Result<IReadOnlyList<SalaryEligibleUserDto>>>
{
    private readonly ISalaryQueries _salary;

    public GetEligibleUsersQueryHandler(ISalaryQueries salary) => _salary = salary;

    public async Task<Result<IReadOnlyList<SalaryEligibleUserDto>>> Handle(
        GetEligibleUsersQuery request, CancellationToken cancellationToken) =>
        Result.Success(await _salary.GetEligibleUsersAsync(cancellationToken));
}
