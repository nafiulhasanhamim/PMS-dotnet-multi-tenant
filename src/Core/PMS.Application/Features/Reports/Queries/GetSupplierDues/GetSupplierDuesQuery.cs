using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Reports;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Reports.Queries.GetSupplierDues;

/// <summary>
/// What each supplier is owed. Module 4's retrofit to Module 8.
/// </summary>
/// <param name="AllTime">
/// The default. A dues report is normally read as "who do we owe right now", and that question has
/// no date range — so the dates are opt-in rather than defaulted to a rolling window the way every
/// other report here does it.
/// </param>
public sealed record GetSupplierDuesQuery(DateOnly? From, DateOnly? To, bool AllTime)
    : IRequest<Result<SupplierDuesReportDto>>, ITenantScopedRequest;

public sealed class GetSupplierDuesQueryHandler
    : IRequestHandler<GetSupplierDuesQuery, Result<SupplierDuesReportDto>>
{
    private readonly IReportQueries _reports;
    private readonly IDateTime _clock;

    public GetSupplierDuesQueryHandler(IReportQueries reports, IDateTime clock)
    {
        _reports = reports;
        _clock = clock;
    }

    public async Task<Result<SupplierDuesReportDto>> Handle(
        GetSupplierDuesQuery request, CancellationToken cancellationToken)
    {
        // All time unless a date was actually supplied. Note this deliberately does NOT fall back
        // to ReportRange's rolling thirty days: a balance narrowed to a month is not a debt, and
        // silently defaulting to one would put a wrong number under a column headed "outstanding".
        var allTime = request.AllTime || (request.From is null && request.To is null);

        DateOnly? from = null;
        DateOnly? to = null;

        if (!allTime)
        {
            var resolved = ReportRange.Resolve(request.From, request.To, _clock.UtcDateToday());
            from = resolved.From;
            to = resolved.To;
        }

        return Result.Success(
            await _reports.GetSupplierDuesAsync(from, to, allTime, cancellationToken));
    }
}
