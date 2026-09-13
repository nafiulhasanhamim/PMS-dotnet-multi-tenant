using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Salary.Commands.RecordSalaryAdvance;

/// <summary>
/// Records cash handed to an employee mid-month, to come out of a later salary.
///
/// <para><b>Recorded when it is given, which is the whole point of the screen.</b> An advance
/// typed in as a number at month-end depends on somebody recalling a note handed over three weeks
/// earlier - and when they do not, the employee is paid in full on top of money they have already
/// had. It also means the cash is counted as an expense on the day it actually left the register.
/// See <c>SalaryAdvance</c>.</para>
/// </summary>
/// <param name="AdvanceDate">Defaults to today when omitted.</param>
public sealed record RecordSalaryAdvanceCommand(
    Guid ProfileId,
    decimal Amount,
    DateOnly? AdvanceDate,
    string? Reason)
    : IRequest<Result<SalaryAdvanceRecordedDto>>, ITenantScopedRequest;
