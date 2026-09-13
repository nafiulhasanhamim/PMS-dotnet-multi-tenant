using PMS.Application.Common.DTOs;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Tenants.Commands.UpdateTenantStatus;

/// <summary>Suspends or restores a pharmacy. Platform-level.</summary>
public sealed record UpdateTenantStatusCommand(Guid TenantId, TenantStatus Status)
    : IRequest<Result<TenantDto>>;
