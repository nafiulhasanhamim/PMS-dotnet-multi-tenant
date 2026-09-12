using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Queries.GetSalaryProfileOptions;

/// <summary>
/// Active profiles for the advance form's employee picker, each carrying its outstanding total.
///
/// <para>The total travels with the option so the form can say "2,000 already outstanding" the
/// moment somebody is chosen. Somebody about to hand over more cash should see what is already
/// owed, and a second round-trip to find out would mean it only appeared sometimes.</para>
/// </summary>
public sealed record GetSalaryProfileOptionsQuery
    : IRequest<Result<IReadOnlyList<SalaryProfileOptionDto>>>, ITenantScopedRequest;

public sealed class GetSalaryProfileOptionsQueryHandler
    : IRequestHandler<GetSalaryProfileOptionsQuery, Result<IReadOnlyList<SalaryProfileOptionDto>>>
{
    private readonly ISalaryQueries _salary;

    public GetSalaryProfileOptionsQueryHandler(ISalaryQueries salary) => _salary = salary;

    public async Task<Result<IReadOnlyList<SalaryProfileOptionDto>>> Handle(
        GetSalaryProfileOptionsQuery request, CancellationToken cancellationToken) =>
        Result.Success(await _salary.GetProfileOptionsAsync(cancellationToken));
}
