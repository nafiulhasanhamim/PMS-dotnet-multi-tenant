using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Commands.MarkSalaryPaid;

/// <summary>
/// Records that a salary has actually been handed over.
///
/// <para><b>Two things happen at once, and the second is easy to miss.</b> The entry locks - no
/// further edits, ever - and it becomes an operating expense <em>on the payment date</em>. An
/// August salary marked paid on 2 September lands in September's profit and loss, not August's.
/// That is cash-basis accounting, it is deliberate, and the module doc explains it in the terms a
/// pharmacy owner will ask about.</para>
/// </summary>
/// <param name="PaymentDate">Defaults to today when omitted.</param>
public sealed record MarkSalaryPaidCommand(Guid EntryId, DateOnly? PaymentDate)
    : IRequest<Result<SalaryEntryRowDto>>, ITenantScopedRequest;
