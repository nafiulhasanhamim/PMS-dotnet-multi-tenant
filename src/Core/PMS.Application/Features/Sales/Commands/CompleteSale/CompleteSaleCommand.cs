using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Sales.Commands.CompleteSale;

/// <summary>
/// Rings up a cart: one sale, its lines, and the stock that left the shelves.
///
/// <para><b>Everything is re-derived here and nothing is taken from the client.</b> The request
/// carries product ids, quantities and unit levels — no prices, no totals, no batch ids. A
/// client that could name its own prices could name lower ones, and a client that could name
/// batches could sell stock that was never on the shelf. The prices come from the product, the
/// batches from the FEFO helper, and every total from <c>SaleMath</c>.</para>
///
/// <para><b>Stock is validated twice, and the second time is the one that counts.</b> The
/// billing screen checks availability when an item goes into the cart; by the time the cashier
/// presses Complete, another till may have sold the same batch or a stock correction may have
/// reduced it. So the check runs again inside the transaction, against freshly read batches, and
/// a shortfall refuses the whole sale rather than quietly selling less.</para>
/// </summary>
/// <param name="Items">
/// The cart. Two entries for the same product and unit level are allowed — a cashier can add
/// the same thing twice — and are filled from one FEFO walk so the second does not read stale
/// quantities.
/// </param>
/// <param name="DiscountType">Null for no discount. One bill-level discount per sale.</param>
/// <param name="DiscountValue">5 for 5%, or 62 for a flat 62 taka. Null with no discount.</param>
/// <param name="Prescription">
/// Required when the cart contains an antibiotic, ignored otherwise. Enforced server-side
/// whatever the client sent — see the handler.
/// </param>
public sealed record CompleteSaleCommand(
    IReadOnlyList<CartItemRequest> Items,
    DiscountType? DiscountType,
    decimal? DiscountValue,
    decimal CashReceived,
    string? CustomerName,
    string? CustomerPhone,
    PrescriptionRequest? Prescription)
    : IRequest<Result<SaleCompletedDto>>, ITenantScopedRequest;
