using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Antibiotics.Queries.GetRegister;

/// <summary>
/// One page of the antibiotic register, with the summary line and the filter dropdowns that go
/// with it.
/// </summary>
/// <param name="From">Null defaults to the first of the current month.</param>
/// <param name="To">Null defaults to today.</param>
public sealed record GetRegisterQuery(
    DateOnly? From,
    DateOnly? To,
    Guid? ProductId,
    string? DoctorName,
    Guid? CashierUserId,
    PrescriptionStatusFilter PrescriptionStatus,
    int Page,
    int PageSize)
    : IRequest<Result<AntibioticRegisterPageDto>>, ITenantScopedRequest;
