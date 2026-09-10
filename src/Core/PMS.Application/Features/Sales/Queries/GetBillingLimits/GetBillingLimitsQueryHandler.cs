using MediatR;
using PMS.Application.Common.Billing;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Security;
using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Sales.Queries.GetBillingLimits;

public sealed class GetBillingLimitsQueryHandler
    : IRequestHandler<GetBillingLimitsQuery, Result<BillingLimitsDto>>
{
    private readonly ICurrentUserService _currentUser;

    public GetBillingLimitsQueryHandler(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public Task<Result<BillingLimitsDto>> Handle(
        GetBillingLimitsQuery request, CancellationToken cancellationToken)
    {
        var role = _currentUser.TenantRole();

        if (role is null)
        {
            return Task.FromResult(
                Result.Failure<BillingLimitsDto>(Error.Unauthorized("Not signed in.")));
        }

        // Every figure comes from BillingPolicy, which is also what the completion handler
        // enforces. One source, two readers — which is the whole reason this endpoint exists
        // rather than a constant in the web app.
        var limits = new BillingLimitsDto(
            BillingPolicy.MaxDiscountPercentFor(role.Value),
            BillingPolicy.DescribeCap(role.Value),
            role is UserRole.Admin or UserRole.Pharmacist,
            role is UserRole.Admin or UserRole.Pharmacist,
            role is UserRole.Admin);

        return Task.FromResult(Result.Success(limits));
    }
}
