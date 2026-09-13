using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Commands.UpdateSalaryEntry;

/// <summary>
/// Corrects a generated salary that has not been paid yet.
///
/// <para><b>Refused once the entry is paid.</b> The money has gone and the employee has a slip;
/// an entry that changes afterwards is not a record of anything. A correction after payment is a
/// deliberate database intervention, documented in the module doc, not a screen.</para>
///
/// <para>The base salary is not editable here at all. It was copied from the profile at
/// generation and is what the employee earned that month; changing it would be inventing a
/// different past. Edit the profile if the figure itself was wrong, and delete-and-regenerate if
/// this month must reflect it.</para>
/// </summary>
public sealed record UpdateSalaryEntryCommand(
    Guid EntryId,
    decimal Bonus,
    decimal AdvanceDeduction,
    decimal OtherDeduction,
    string? AdjustmentNotes)
    : IRequest<Result<SalaryEntryRowDto>>, ITenantScopedRequest;
