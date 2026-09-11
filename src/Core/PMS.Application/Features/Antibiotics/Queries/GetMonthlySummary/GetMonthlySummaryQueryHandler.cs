using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Antibiotics.Queries.GetMonthlySummary;

public sealed class GetMonthlySummaryQueryHandler
    : IRequestHandler<GetMonthlySummaryQuery, Result<AntibioticMonthlySummaryDto>>
{
    private readonly IAntibioticQueries _antibiotics;
    private readonly IDateTime _clock;

    public GetMonthlySummaryQueryHandler(IAntibioticQueries antibiotics, IDateTime clock)
    {
        _antibiotics = antibiotics;
        _clock = clock;
    }

    public async Task<Result<AntibioticMonthlySummaryDto>> Handle(
        GetMonthlySummaryQuery request, CancellationToken cancellationToken)
    {
        var today = _clock.UtcDateToday();

        // Clamped rather than validated. A dashboard card asking for month 13 is a bug in a
        // caller, and answering with the current month is more useful than a 400 on a panel
        // nobody was looking at.
        var month = request.Month is >= 1 and <= 12 ? request.Month.Value : today.Month;
        var year = request.Year is >= 2000 and <= 2200 ? request.Year.Value : today.Year;

        return Result.Success(
            await _antibiotics.GetMonthlySummaryAsync(month, year, cancellationToken));
    }
}
