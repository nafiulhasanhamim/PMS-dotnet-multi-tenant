using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Purchases.Commands.CreatePurchaseReturn;

/// <summary>
/// Sends goods back to a supplier, against one line of one purchase.
///
/// <para>Admin and Pharmacist. Unlike a payment, this is a stock event: somebody is holding a
/// damaged box and needs to record where it went, and making them find an Admin first is how
/// stock records stop matching the shelf.</para>
/// </summary>
/// <param name="Quantity">
/// In whatever unit the person was holding, like everywhere else in this system. The handler
/// converts through Module 2's helpers rather than making the client do packing arithmetic.
/// </param>
public sealed record CreatePurchaseReturnCommand(
    Guid PurchaseId,
    Guid PurchaseLineId,
    decimal Quantity,
    UnitLevel QuantityUnit,
    string Reason)
    : IRequest<Result<PurchaseReturnedDto>>, ITenantScopedRequest;
