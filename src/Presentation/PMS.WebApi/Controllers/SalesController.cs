using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.DTOs;
using PMS.Application.Features.Sales.Commands.CancelSale;
using PMS.Application.Features.Sales.Commands.CompleteSale;
using PMS.Application.Features.Sales.Commands.CreateSalesReturn;
using PMS.Application.Features.Sales.Queries.GetBillingLimits;
using PMS.Application.Features.Sales.Queries.GetCashiers;
using PMS.Application.Features.Sales.Queries.GetReturnableLines;
using PMS.Application.Features.Sales.Queries.GetSale;
using PMS.Application.Features.Sales.Queries.GetSales;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Grid;
using PMS.WebApi.Extensions;

namespace PMS.WebApi.Controllers;

/// <summary>
/// The till: completing a sale, reading invoices back, returns and cancellations.
///
/// <para>Every action is tenant-scoped and none of them mentions a tenant. The commands and
/// queries carry <c>ITenantScopedRequest</c>, so the pipeline refuses to run one without a
/// resolved pharmacy, and the global query filter supplies the WHERE — which is what makes a
/// sale rung up in one pharmacy invisible in another, by list or by direct id.</para>
///
/// <para><b>Access, and why it is split three ways rather than two.</b> Selling is the one
/// thing every role does, including an Employee — refusing them the till would leave the
/// pharmacy unable to staff a counter. Returns are Admin and Pharmacist: money going back out
/// of the drawer is not a counter decision. Cancelling is Admin alone, because it reverses a
/// whole transaction and removes it from every report, which is the closest thing in this
/// system to erasing a day's takings.</para>
///
/// <para>Two further restrictions are enforced inside the handlers rather than by a policy,
/// because a policy cannot see them: an Employee may sell no antibiotics, and may list and open
/// only sales they rang up themselves. Both are declared on the platform access page as
/// withheld, so they are visible in the access review rather than buried here.</para>
/// </summary>
[Route("api/sales")]
[Authorize(Policy = AuthenticationExtensions.TenantUserPolicy)]
public class SalesController : ApiControllerBase
{
    /// <summary>
    /// One page of sales, newest first.
    ///
    /// <para>An Employee sees only their own, and the restriction is applied in the query
    /// rather than to the results — so the row count is theirs too, and there is no page to
    /// scroll to that holds anybody else's takings.</para>
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(GridResult<SaleListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSales(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] Guid? cashier = null,
        [FromQuery] SaleStatusFilter status = SaleStatusFilter.All,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetSalesQuery(from, to, cashier, status, page, pageSize), cancellationToken));

    /// <summary>
    /// What the signed-in caller may do at the till: their discount cap, and whether they may
    /// dispense antibiotics, take returns or cancel a sale.
    ///
    /// <para>The billing screen reads this to render "Max discount: 10%" and to pre-validate
    /// before submitting. It is a convenience, not a control — <c>BillingPolicy</c> refuses the
    /// sale server-side regardless, and this endpoint reads the same class, so the number on
    /// screen cannot drift from the number enforced.</para>
    /// </summary>
    [HttpGet("limits")]
    [ProducesResponseType(typeof(BillingLimitsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLimits(CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetBillingLimitsQuery(), cancellationToken));

    /// <summary>
    /// The cashiers who appear in this pharmacy's sales, for the list filter.
    ///
    /// <para>Empty for an Employee, who has nothing to filter by. Drawn from the sales rather
    /// than from the staff list, so it never offers somebody with no sales and never omits a
    /// cashier who has since left.</para>
    /// </summary>
    [HttpGet("cashiers")]
    [ProducesResponseType(typeof(IReadOnlyList<CashierOptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCashiers(CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetCashiersQuery(), cancellationToken));

    /// <summary>
    /// One sale in full: grouped as the customer's invoice, and itemised per batch for staff.
    ///
    /// <para>Every money figure comes from the sale's own rows, never from the product's
    /// current price. Re-pricing a product tomorrow does not change what this returns.</para>
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SaleDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSale(
        Guid id, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetSaleQuery(id), cancellationToken));

    /// <summary>
    /// What can still be returned on each line, and what returning it would refund.
    ///
    /// <b>Admin or Pharmacist</b>, the same pair who may act on it — a screen an Employee could
    /// read but never use would only invite them to start.
    /// </summary>
    [HttpGet("{id:guid}/returnable-lines")]
    [Authorize(Policy = AuthenticationExtensions.TenantWriterPolicy)]
    [ProducesResponseType(typeof(ReturnableSaleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetReturnableLines(
        Guid id, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetReturnableLinesQuery(id), cancellationToken));

    /// <summary>
    /// Completes a sale. Every role.
    ///
    /// <para>The body carries product ids, quantities and unit levels — no prices, no totals
    /// and no batch ids. Prices come from the product, batches from FEFO, and every total from
    /// the domain's own arithmetic; a client that could name any of them could name better
    /// ones. Stock is re-validated inside the transaction, so a batch sold out by another till
    /// since the item went into the cart refuses the sale rather than overselling it.</para>
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SaleCompletedDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteSale(
        [FromBody] CompleteSaleRequest request, CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(
            new CompleteSaleCommand(
                request.Items ?? [],
                request.DiscountType,
                request.DiscountValue,
                request.CashReceived,
                request.CustomerName,
                request.CustomerPhone,
                request.Prescription),
            cancellationToken);

        return HandleCreatedResult(result, nameof(GetSale), sale => new { id = sale.Id });
    }

    /// <summary>
    /// Reverses a whole sale and puts its stock back. <b>Admin only.</b>
    ///
    /// <para>A Pharmacist gets 403 here, and the sales list hides the action for them — the
    /// hiding is a courtesy, this is the control.</para>
    ///
    /// <para>Stock returns to the batches each line came out of, through
    /// <c>StockAdjustment</c> rows tagged "Sale cancelled: {reason}", so a shelf count next
    /// week has an explanation for the extra units. Units a return has already put back are
    /// not restored a second time.</para>
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = AuthenticationExtensions.TenantAdminPolicy)]
    [ProducesResponseType(typeof(CancelledSaleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelSale(
        Guid id,
        [FromBody] CancelSaleRequest request,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new CancelSaleCommand(id, request.Reason), cancellationToken));

    /// <summary>
    /// Takes goods back against one line and refunds the customer. <b>Admin or Pharmacist.</b>
    ///
    /// <para>The refund is computed from the line's discounted total, not its sticker price, so
    /// a return against a discounted sale pays back what the customer actually paid. Stock goes
    /// to the batch the line deducted from, with an adjustment row tagged
    /// "Sales return: {reason}".</para>
    /// </summary>
    [HttpPost("{id:guid}/returns")]
    [Authorize(Policy = AuthenticationExtensions.TenantWriterPolicy)]
    [ProducesResponseType(typeof(SalesReturnedDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateReturn(
        Guid id,
        [FromBody] CreateReturnRequest request,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new CreateSalesReturnCommand(
                id, request.SaleLineId, request.Quantity, request.QuantityUnit, request.Reason),
            cancellationToken));
}

/// <summary>
/// The body of a sale.
///
/// <para>A request record rather than binding the command directly, because the sale id in the
/// route has no counterpart here and the command is the shape the handler wants rather than the
/// shape the wire has. Note again what is missing: no prices, no line totals, no batches.</para>
/// </summary>
public sealed record CompleteSaleRequest(
    IReadOnlyList<CartItemRequest>? Items,
    DiscountType? DiscountType,
    decimal? DiscountValue,
    decimal CashReceived,
    string? CustomerName,
    string? CustomerPhone,
    PrescriptionRequest? Prescription);

/// <param name="Reason">Required, free text. "Why was invoice 452 cancelled" needs an answer.</param>
public sealed record CancelSaleRequest(string Reason);

/// <param name="Quantity">In <paramref name="QuantityUnit"/>, so a strip can be handed back as one.</param>
public sealed record CreateReturnRequest(
    Guid SaleLineId,
    decimal Quantity,
    UnitLevel QuantityUnit,
    string Reason);
