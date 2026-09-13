using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Purchases.Commands.CreatePurchase;

/// <summary>
/// Records a delivery against a supplier's bill, creating one batch per line.
///
/// <para><b>Quantity and price arrive in whatever unit the person was holding</b> — 20 strips at
/// ৳8 a strip, 2 cartons at ৳4,320 a carton — exactly as <c>CreateBatchCommand</c> takes them,
/// because each line <em>is</em> a <c>CreateBatchCommand</c>. This command does not convert, does
/// not check batch numbers and does not decide whether an expiry is required: it forwards each
/// line to Module 3 and lets the one implementation of those rules answer.</para>
/// </summary>
public sealed record CreatePurchaseCommand(
    Guid SupplierId,
    DateOnly? PurchaseDate,
    string? Notes,
    IReadOnlyList<CreatePurchaseLine> Lines)
    : IRequest<Result<PurchaseCreatedDto>>, ITenantScopedRequest;

/// <summary>
/// One line of a purchase — which is one delivery of one product, and therefore one batch.
///
/// <para>Shaped to match <c>CreateBatchCommand</c> field for field, deliberately: the handler
/// maps this onto that with no arithmetic in between, so there is nothing here that could
/// interpret a unit differently from the way Add Stock does.</para>
/// </summary>
public sealed record CreatePurchaseLine(
    Guid ProductId,
    string BatchNumber,
    DateOnly? ExpiryDate,
    DateOnly? ManufactureDate,
    decimal Quantity,
    UnitLevel QuantityUnit,
    decimal PurchasePrice,
    UnitLevel PurchasePriceUnit,
    string? Notes);
