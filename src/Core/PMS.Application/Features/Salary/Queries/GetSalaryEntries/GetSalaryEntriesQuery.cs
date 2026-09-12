using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Salary;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Queries.GetSalaryEntries;

/// <summary>One page of generated salaries. Newest period first, then by employee name.</summary>
public sealed record GetSalaryEntriesQuery(
    int? Month,
    int? Year,
    Guid? ProfileId,
    SalaryStatusFilter Status,
    int Page,
    int PageSize)
    : IRequest<Result<GridResult<SalaryEntryRowDto>>>, ITenantScopedRequest;

public sealed class GetSalaryEntriesQueryHandler
    : IRequestHandler<GetSalaryEntriesQuery, Result<GridResult<SalaryEntryRowDto>>>
{
    private readonly ISalaryQueries _salary;

    public GetSalaryEntriesQueryHandler(ISalaryQueries salary) => _salary = salary;

    public async Task<Result<GridResult<SalaryEntryRowDto>>> Handle(
        GetSalaryEntriesQuery request, CancellationToken cancellationToken)
    {
        // An out-of-range month here is a FILTER, not a period to create, so it is dropped rather
        // than refused: a history screen with a nonsense month in the query string should show
        // everything, not an error page.
        var month = request.Month is { } m && SalaryPeriod.IsValidMonth(m) ? m : (int?)null;
        var year = request.Year is { } y && SalaryPeriod.IsValidYear(y) ? y : (int?)null;

        return Result.Success(await _salary.GetEntriesAsync(
            month, year, request.ProfileId, request.Status,
            SalaryPaging.Page(request.Page), SalaryPaging.Size(request.PageSize),
            cancellationToken));
    }
}
