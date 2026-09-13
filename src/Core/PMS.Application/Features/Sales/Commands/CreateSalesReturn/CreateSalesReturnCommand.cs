using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Sales.Commands.CreateSalesReturn;

/// <summary>
/// Takes goods back against one sale line and refunds the customer. Admin or Pharmacist.
///
/// <para><b>Against a line, not against a sale.</b> The line is what knows the batch to restore
/// and the discounted price the customer actually paid, and a return that named only the product
/// could work out neither.</para>
/// </summary>
/// <param name="Quantity">
/// In <paramref name="QuantityUnit"/>, so a cashier can hand back "1 strip" of something sold in
/// pieces. Converted here, against the product's own unit configuration.
/// </param>
public sealed record CreateSalesReturnCommand(
    Guid SaleId,
    Guid SaleLineId,
    decimal Quantity,
    UnitLevel QuantityUnit,
    string Reason)
    : IRequest<Result<SalesReturnedDto>>, ITenantScopedRequest;
