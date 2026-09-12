using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Queries.GetSalaryProfile;

/// <summary>One profile, for the edit form.</summary>
public sealed record GetSalaryProfileQuery(Guid ProfileId)
    : IRequest<Result<SalaryProfileDetailDto>>, ITenantScopedRequest;

public sealed class GetSalaryProfileQueryHandler
    : IRequestHandler<GetSalaryProfileQuery, Result<SalaryProfileDetailDto>>
{
    private readonly ISalaryQueries _salary;

    public GetSalaryProfileQueryHandler(ISalaryQueries salary) => _salary = salary;

    public async Task<Result<SalaryProfileDetailDto>> Handle(
        GetSalaryProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await _salary.GetProfileAsync(request.ProfileId, cancellationToken);

        return profile is null
            ? Result.Failure<SalaryProfileDetailDto>(
                Error.NotFound(nameof(EmployeeSalaryProfile), request.ProfileId))
            : Result.Success(profile);
    }
}
