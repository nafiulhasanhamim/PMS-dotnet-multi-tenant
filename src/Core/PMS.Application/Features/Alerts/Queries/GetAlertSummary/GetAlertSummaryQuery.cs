using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Alerts.Queries.GetAlertSummary;

/// <summary>
/// The dashboard counts.
///
/// <para>Takes no window: the caller does not get to choose one here, because the four cards and
/// the sidebar badge have to agree with each other and with the default the pages open on. The
/// expiring-soon <em>page</em> is where a window is selectable.</para>
/// </summary>
public sealed record GetAlertSummaryQuery
    : IRequest<Result<AlertSummaryDto>>, ITenantScopedRequest;
