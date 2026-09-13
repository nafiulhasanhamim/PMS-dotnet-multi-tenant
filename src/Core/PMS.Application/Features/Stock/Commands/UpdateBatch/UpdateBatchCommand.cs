using PMS.Application.Common.DTOs;
using PMS.Application.Common.Stock;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Stock.Commands.UpdateBatch;

/// <summary>
/// Corrects a batch's details — the things that can be mistyped off a pack.
///
/// <para><b>Quantity is not among them.</b> Every change to a quantity has to arrive with a
/// reason attached, and this endpoint has nowhere to put one. See
/// <c>AdjustBatchCommand</c>.</para>
/// </summary>
/// <param name="QuantityInBaseUnits">
/// Accepted only so that an attempt to change the quantity here can be <b>refused out loud</b>.
/// A field silently ignored is worse than one that errors: a client would post a new quantity,
/// get a 200 and a batch DTO back, and reasonably conclude the change took effect. Sending the
/// current value is accepted as the no-op it is, so a client that echoes the whole object back
/// is not punished for it.
/// </param>
public sealed record UpdateBatchCommand(
    Guid Id,
    string BatchNumber,
    DateOnly? ExpiryDate,
    DateOnly? ManufactureDate,
    decimal PurchasePrice,
    UnitLevel PurchasePriceUnit,
    Guid? SupplierId,
    string? SupplierNameText,
    string? Notes,
    int? QuantityInBaseUnits)
    : IRequest<Result<BatchDto>>, ITenantScopedRequest, IBatchWriteRequest;
