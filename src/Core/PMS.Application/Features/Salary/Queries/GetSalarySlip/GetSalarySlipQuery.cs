using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Queries.GetSalarySlip;

/// <summary>
/// Everything the printable slip needs, including which advances this entry recovered.
///
/// <para>An unpaid entry has a slip too. Handing somebody the breakdown before the money moves is
/// how a disagreement gets settled before it becomes one.</para>
/// </summary>
public sealed record GetSalarySlipQuery(Guid EntryId)
    : IRequest<Result<SalarySlipDto>>, ITenantScopedRequest;

public sealed class GetSalarySlipQueryHandler
    : IRequestHandler<GetSalarySlipQuery, Result<SalarySlipDto>>
{
    private readonly ISalaryQueries _salary;

    public GetSalarySlipQueryHandler(ISalaryQueries salary) => _salary = salary;

    public async Task<Result<SalarySlipDto>> Handle(
        GetSalarySlipQuery request, CancellationToken cancellationToken)
    {
        var slip = await _salary.GetSlipAsync(request.EntryId, cancellationToken);

        return slip is null
            ? Result.Failure<SalarySlipDto>(Error.NotFound(nameof(SalaryEntry), request.EntryId))
            : Result.Success(slip);
    }
}
