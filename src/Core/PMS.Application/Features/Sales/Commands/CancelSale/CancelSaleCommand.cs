using MediatR;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Sales.Commands.CancelSale;

/// <summary>
/// Reverses a whole sale and puts its stock back. <b>Admin only</b> — enforced by the endpoint's
/// policy, not here.
///
/// <para>The reason is required and free text. "Why was invoice 452 cancelled" is a question an
/// owner asks about their own staff, and a fixed list of reasons would be answered with whatever
/// option is least trouble to click.</para>
/// </summary>
public sealed record CancelSaleCommand(Guid SaleId, string Reason)
    : IRequest<Result<CancelledSaleDto>>, ITenantScopedRequest;

/// <summary>What a cancellation reports back, including where the stock went.</summary>
/// <param name="RestoredInBaseUnits">
/// Total base units put back. Less than the sale's quantity when part of it had already been
/// returned — see the handler for why that is the correct figure rather than a bug.
/// </param>
public sealed record CancelledSaleDto(
    Guid SaleId,
    string InvoiceNumber,
    decimal NetTotal,
    int LinesRestored,
    int RestoredInBaseUnits,
    int SkippedAlreadyReturned);
