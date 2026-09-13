using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Tenants.Commands.UpdateTenantStatus;

public sealed class UpdateTenantStatusCommandHandler
    : IRequestHandler<UpdateTenantStatusCommand, Result<TenantDto>>
{
    private readonly IIdentityQueries _identity;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ILogger<UpdateTenantStatusCommandHandler> _logger;

    public UpdateTenantStatusCommandHandler(
        IIdentityQueries identity,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ILogger<UpdateTenantStatusCommandHandler> logger)
    {
        _identity = identity;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<TenantDto>> Handle(
        UpdateTenantStatusCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _identity.FindTenantByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            _logger.LogWarning(
                "Tenant status not changed: no tenant {TenantId} exists", request.TenantId);

            return Result.Failure<TenantDto>(Error.NotFound(nameof(Tenant), request.TenantId));
        }

        var previous = tenant.Status;

        tenant.SetStatus(request.Status);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The most disruptive single line in the platform, and the reason it is logged with
        // its previous value: TenantResolutionMiddleware rechecks status on every request, so
        // a suspension throws out sessions that are already signed in, immediately. Everyone
        // at that pharmacy is locked out mid-shift with a generic message, and the refusals
        // they generate are logged by TenantLoginCommandHandler as "pharmacy is Suspended".
        // This is the line that says who did it and when, which is what turns a flood of
        // login failures into an explanation.
        _logger.LogInformation(
            "Tenant {TenantName} ({TenantId}) status changed {PreviousStatus} -> {NewStatus}; "
            + "takes effect immediately for sessions already signed in",
            tenant.Name, tenant.Id, previous, tenant.Status);

        return new TenantDto(tenant.Id, tenant.Name, tenant.DomainName, tenant.Status,
            tenant.SubscriptionPlan, tenant.CreatedOnUtc);
    }
}
