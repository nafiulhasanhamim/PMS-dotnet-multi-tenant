using MediatR;
using PMS.Application.Common.Billing;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Security;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Sales.Queries.GetBillingLimits;

public sealed class GetBillingLimitsQueryHandler
    : IRequestHandler<GetBillingLimitsQuery, Result<BillingLimitsDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantSettings _settings;

    public GetBillingLimitsQueryHandler(
        ICurrentUserService currentUser, ITenantSettings settings)
    {
        _currentUser = currentUser;
        _settings = settings;
    }

    public async Task<Result<BillingLimitsDto>> Handle(
        GetBillingLimitsQuery request, CancellationToken cancellationToken)
    {
        var role = _currentUser.TenantRole();

        if (role is null)
        {
            return Result.Failure<BillingLimitsDto>(Error.Unauthorized("Not signed in."));
        }

        var mode = await _settings.GetAntibioticModeAsync(cancellationToken);

        // Every figure comes from BillingPolicy or ITenantSettings, which are also what the
        // completion handler enforces. One source, two readers — the whole reason this endpoint
        // exists rather than a constant in the web app.
        var limits = new BillingLimitsDto(
            BillingPolicy.MaxDiscountPercentFor(role.Value),
            BillingPolicy.DescribeCap(role.Value),

            // Role and mode together. Only Required keeps an Employee away from antibiotics;
            // this mirrors CompleteSaleCommandHandler.Blocked exactly, and the sale is refused
            // server-side regardless of what this says.
            BillingPolicy.MaySellAntibiotics(role.Value, mode),
            role is UserRole.Admin or UserRole.Pharmacist,
            role is UserRole.Admin,
            mode);

        return Result.Success(limits);
    }
}
