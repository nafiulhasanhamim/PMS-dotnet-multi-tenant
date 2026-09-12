using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Queries.GetSalarySummary;

/// <summary>The hub's summary figures: what is owed to staff, and what staff owe back.</summary>
public sealed record GetSalarySummaryQuery
    : IRequest<Result<SalarySummaryDto>>, ITenantScopedRequest;

public sealed class GetSalarySummaryQueryHandler
    : IRequestHandler<GetSalarySummaryQuery, Result<SalarySummaryDto>>
{
    private readonly ISalaryQueries _salary;

    public GetSalarySummaryQueryHandler(ISalaryQueries salary) => _salary = salary;

    public async Task<Result<SalarySummaryDto>> Handle(
        GetSalarySummaryQuery request, CancellationToken cancellationToken) =>
        Result.Success(await _salary.GetSummaryAsync(cancellationToken));
}
