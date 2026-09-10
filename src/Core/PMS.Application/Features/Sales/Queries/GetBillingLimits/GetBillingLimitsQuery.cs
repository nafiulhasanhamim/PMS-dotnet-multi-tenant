using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Sales.Queries.GetBillingLimits;

/// <summary>
/// What the signed-in caller may do at the till. Takes no parameters on purpose: the answer is
/// about the token, and a caller cannot ask about somebody else's permissions.
/// </summary>
public sealed record GetBillingLimitsQuery
    : IRequest<Result<BillingLimitsDto>>, ITenantScopedRequest;
