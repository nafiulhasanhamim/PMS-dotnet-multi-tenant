using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Reports.Queries.GetMonthlySalesReport;

/// <summary>One month, day by day, with net profit.</summary>
/// <param name="Month">1-12. Null, like a null year, falls back to the current month.</param>
public sealed record GetMonthlySalesReportQuery(int? Month, int? Year)
    : IRequest<Result<MonthlySalesReportDto>>, ITenantScopedRequest;
