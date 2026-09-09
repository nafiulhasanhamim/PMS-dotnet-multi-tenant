using PMS.Application.Common.DTOs;
using PMS.Application.Common.Stock;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Stock.Commands.CreateBatch;

/// <summary>
/// Records one delivery of one product.
///
/// <para><b>Quantity and price arrive in whatever unit the person was holding</b> — 20 strips
/// at ৳8 a strip, 2 cartons at ৳4,320 a carton — together with which level that is. The
/// handler converts both to base units through Module 2's helpers before anything is
/// persisted, so the stored row is always 200 pieces at ৳0.80. Accepting the entered unit
/// rather than making the client convert is deliberate: a client that did its own arithmetic
/// would be a second implementation of the two-level packing rule, which is the one piece of
/// arithmetic in this system most likely to be got wrong.</para>
///
/// <para>Module 4 (Purchase) will send this same command rather than writing batches itself.
/// A purchase line <em>is</em> a delivery, and duplicating the conversion, the duplicate-number
/// policy and the loss check would guarantee the two paths drifted.</para>
/// </summary>
/// <param name="Quantity">
/// Decimal because a form may offer "1.5 boxes", though it must resolve to whole base units.
/// </param>
public sealed record CreateBatchCommand(
    Guid ProductId,
    string BatchNumber,
    DateOnly? ExpiryDate,
    DateOnly? ManufactureDate,
    decimal Quantity,
    UnitLevel QuantityUnit,
    decimal PurchasePrice,
    UnitLevel PurchasePriceUnit,
    Guid? SupplierId,
    string? SupplierNameText,
    string? Notes)
    : IRequest<Result<BatchCreatedDto>>, ITenantScopedRequest, IBatchWriteRequest;
