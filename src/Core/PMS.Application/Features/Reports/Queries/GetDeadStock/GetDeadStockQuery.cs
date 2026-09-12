using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Reports.Queries.GetDeadStock;

/// <summary>
/// Stock that has not sold in a while, or ever, with the totals for the whole filtered set.
///
/// <para>Rows and summary in one request, for the reason the antibiotic register gives: a header
/// reading "12 products, 45,000 tied up" that disagrees with the table under it is worse than no
/// header at all.</para>
/// </summary>
public sealed record GetDeadStockQuery(
    int? ThresholdDays, ProductType? ProductType, int Page, int PageSize)
    : IRequest<Result<DeadStockPageDto>>, ITenantScopedRequest;
