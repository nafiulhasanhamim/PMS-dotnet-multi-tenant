using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Salary;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Queries.GetSalaryProfiles;

/// <summary>One page of the payroll, each row with what the employee still owes back.</summary>
public sealed record GetSalaryProfilesQuery(
    string? Search, SalaryProfileStatusFilter Status, int Page, int PageSize)
    : IRequest<Result<GridResult<SalaryProfileListItemDto>>>, ITenantScopedRequest;

public sealed class GetSalaryProfilesQueryHandler
    : IRequestHandler<GetSalaryProfilesQuery, Result<GridResult<SalaryProfileListItemDto>>>
{
    private readonly ISalaryQueries _salary;

    public GetSalaryProfilesQueryHandler(ISalaryQueries salary) => _salary = salary;

    public async Task<Result<GridResult<SalaryProfileListItemDto>>> Handle(
        GetSalaryProfilesQuery request, CancellationToken cancellationToken) =>
        Result.Success(await _salary.GetProfilesAsync(
            request.Search, request.Status,
            SalaryPaging.Page(request.Page), SalaryPaging.Size(request.PageSize),
            cancellationToken));
}
