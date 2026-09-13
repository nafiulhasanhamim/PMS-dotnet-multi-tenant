using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Salary;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Queries.GetGenerationPreview;

/// <summary>
/// Every active profile for a month, with its unsettled advances broken out.
///
/// <para>Profiles already generated come back flagged rather than missing - an employee absent
/// from the list is indistinguishable from one nobody put on the payroll.</para>
/// </summary>
public sealed record GetGenerationPreviewQuery(int Month, int Year)
    : IRequest<Result<SalaryGenerationPreviewDto>>, ITenantScopedRequest;

public sealed class GetGenerationPreviewQueryHandler
    : IRequestHandler<GetGenerationPreviewQuery, Result<SalaryGenerationPreviewDto>>
{
    private readonly ISalaryQueries _salary;

    public GetGenerationPreviewQueryHandler(ISalaryQueries salary) => _salary = salary;

    public async Task<Result<SalaryGenerationPreviewDto>> Handle(
        GetGenerationPreviewQuery request, CancellationToken cancellationToken)
    {
        // Refused rather than clamped. A silently corrected month would generate a payroll for a
        // period nobody asked for, and every entry of it would be locked the moment it was paid.
        if (!SalaryPeriod.IsValid(request.Month, request.Year))
        {
            return Result.Failure<SalaryGenerationPreviewDto>(Error.Validation(
                nameof(GetGenerationPreviewQuery.Month),
                "Choose a month between 1 and 12 and a year between "
                + $"{SalaryPeriod.MinYear} and {SalaryPeriod.MaxYear}."));
        }

        return Result.Success(await _salary.GetGenerationPreviewAsync(
            request.Month, request.Year, cancellationToken));
    }
}
