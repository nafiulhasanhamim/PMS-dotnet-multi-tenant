using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Antibiotics.Queries.GetMonthlySummary;

/// <summary>
/// Total antibiotic quantity dispensed in one month, for the dashboard card.
///
/// <para>Null month or year means the current one — a dashboard should not have to know today's
/// date to ask about today's month.</para>
/// </summary>
public sealed record GetMonthlySummaryQuery(int? Month, int? Year)
    : IRequest<Result<AntibioticMonthlySummaryDto>>, ITenantScopedRequest;
