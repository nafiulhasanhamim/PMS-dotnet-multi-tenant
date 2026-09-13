using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Antibiotics;
using PMS.Application.Common.Stock;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Antibiotics.Queries.GetRegister;

/// <summary>
/// Assembles the register page: the rows, the summary over the whole filtered range, and the
/// filter options.
///
/// <para>One request rather than three, because all three change together. A page that fetched
/// its rows and its total separately could render a total that disagreed with what is on
/// screen — and on a document somebody may hand to an inspector, a header that contradicts the
/// table is worse than no header.</para>
/// </summary>
public sealed class GetRegisterQueryHandler
    : IRequestHandler<GetRegisterQuery, Result<AntibioticRegisterPageDto>>
{
    private readonly IAntibioticQueries _antibiotics;
    private readonly IDateTime _clock;

    public GetRegisterQueryHandler(IAntibioticQueries antibiotics, IDateTime clock)
    {
        _antibiotics = antibiotics;
        _clock = clock;
    }

    public async Task<Result<AntibioticRegisterPageDto>> Handle(
        GetRegisterQuery request, CancellationToken cancellationToken)
    {
        var (from, to) = RegisterRange.Resolve(request.From, request.To, _clock.UtcDateToday());

        var rows = await _antibiotics.GetRegisterAsync(
            from, to, request.ProductId, request.DoctorName, request.CashierUserId,
            request.PrescriptionStatus,
            AlertPaging.Page(request.Page), AlertPaging.Size(request.PageSize),
            cancellationToken);

        var summary = await _antibiotics.GetRegisterSummaryAsync(
            from, to, request.ProductId, request.DoctorName, request.CashierUserId,
            request.PrescriptionStatus, cancellationToken);

        var options = await _antibiotics.GetFilterOptionsAsync(cancellationToken);

        return Result.Success(new AntibioticRegisterPageDto(
            from, to, rows, summary, options));
    }
}
