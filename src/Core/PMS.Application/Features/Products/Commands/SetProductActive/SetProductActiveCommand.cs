using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Products.Commands.SetProductActive;

/// <summary>
/// Deactivates or reactivates a product - a soft delete, Admin only.
///
/// Nothing is removed. Existing stock, past purchases and past sales all still refer to this
/// product, and a hard delete would either orphan them or cascade away a pharmacy's trading
/// history. Deactivating only keeps it out of default lists and new transactions.
/// </summary>
public sealed record SetProductActiveCommand(Guid Id, bool IsActive)
    : IRequest<Result<ProductDto>>, ITenantScopedRequest;
