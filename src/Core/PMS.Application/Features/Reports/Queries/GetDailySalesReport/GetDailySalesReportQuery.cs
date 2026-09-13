using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Reports.Queries.GetDailySalesReport;

/// <summary>One day's sales, profit and hourly breakdown.</summary>
/// <param name="Date">Null means today, so the page opens on something useful.</param>
public sealed record GetDailySalesReportQuery(DateOnly? Date)
    : IRequest<Result<DailySalesReportDto>>, ITenantScopedRequest;
