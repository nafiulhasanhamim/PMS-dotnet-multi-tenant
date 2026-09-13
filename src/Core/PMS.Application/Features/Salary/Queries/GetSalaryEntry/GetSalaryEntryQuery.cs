using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Queries.GetSalaryEntry;

/// <summary>One entry, for the edit form.</summary>
public sealed record GetSalaryEntryQuery(Guid EntryId)
    : IRequest<Result<SalaryEntryRowDto>>, ITenantScopedRequest;

public sealed class GetSalaryEntryQueryHandler
    : IRequestHandler<GetSalaryEntryQuery, Result<SalaryEntryRowDto>>
{
    private readonly ISalaryQueries _salary;

    public GetSalaryEntryQueryHandler(ISalaryQueries salary) => _salary = salary;

    public async Task<Result<SalaryEntryRowDto>> Handle(
        GetSalaryEntryQuery request, CancellationToken cancellationToken)
    {
        var entry = await _salary.GetEntryAsync(request.EntryId, cancellationToken);

        return entry is null
            ? Result.Failure<SalaryEntryRowDto>(
                Error.NotFound(nameof(SalaryEntry), request.EntryId))
            : Result.Success(entry);
    }
}
