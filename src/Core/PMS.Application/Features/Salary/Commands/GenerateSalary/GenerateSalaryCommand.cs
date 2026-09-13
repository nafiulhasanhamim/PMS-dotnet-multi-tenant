using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Commands.GenerateSalary;

/// <summary>
/// Creates a month's salary entries for the selected employees, and settles the advances they
/// cover.
///
/// <para><b>One press, one transaction.</b> Every entry and every advance settlement either
/// happens or none of it does. A half-generated payroll - three entries written, the fourth
/// failing, and an advance marked settled against an entry that does not exist - is the outcome
/// worth the whole of this handler's structure.</para>
///
/// <para>The per-employee <c>AdvanceDeduction</c> is a request rather than a result: the domain
/// caps it and decides which advances it covers. See <c>SalaryEntry.Generate</c>.</para>
/// </summary>
public sealed record GenerateSalaryCommand(
    int Month,
    int Year,
    IReadOnlyList<SalaryGenerationLine> Lines)
    : IRequest<Result<SalaryGenerationResultDto>>, ITenantScopedRequest;
