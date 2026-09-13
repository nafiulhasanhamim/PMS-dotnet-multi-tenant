using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Salary;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Queries.GetSalaryAdvances;

/// <summary>One page of advances, settled and unsettled together, newest first.</summary>
public sealed record GetSalaryAdvancesQuery(
    Guid? ProfileId, AdvanceSettlementFilter Settlement, int Page, int PageSize)
    : IRequest<Result<GridResult<SalaryAdvanceRowDto>>>, ITenantScopedRequest;

public sealed class GetSalaryAdvancesQueryHandler
    : IRequestHandler<GetSalaryAdvancesQuery, Result<GridResult<SalaryAdvanceRowDto>>>
{
    private readonly ISalaryQueries _salary;

    public GetSalaryAdvancesQueryHandler(ISalaryQueries salary) => _salary = salary;

    public async Task<Result<GridResult<SalaryAdvanceRowDto>>> Handle(
        GetSalaryAdvancesQuery request, CancellationToken cancellationToken) =>
        Result.Success(await _salary.GetAdvancesAsync(
            request.ProfileId, request.Settlement,
            SalaryPaging.Page(request.Page), SalaryPaging.Size(request.PageSize),
            cancellationToken));
}
