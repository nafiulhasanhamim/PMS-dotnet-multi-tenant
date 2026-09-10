using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Sales.Queries.GetReturnableLines;

public sealed class GetReturnableLinesQueryHandler
    : IRequestHandler<GetReturnableLinesQuery, Result<ReturnableSaleDto>>
{
    private readonly ISaleQueries _sales;

    public GetReturnableLinesQueryHandler(ISaleQueries sales)
    {
        _sales = sales;
    }

    public async Task<Result<ReturnableSaleDto>> Handle(
        GetReturnableLinesQuery request, CancellationToken cancellationToken)
    {
        var sale = await _sales.FindReturnableAsync(request.SaleId, cancellationToken);

        if (sale is null)
        {
            return Result.Failure<ReturnableSaleDto>(
                Error.NotFound(nameof(Sale), request.SaleId));
        }

        if (sale.Status == SaleStatus.Cancelled)
        {
            // A cancellation has already restored the stock and reversed the money. Answering
            // with a list of returnable lines would invite a return that the command then
            // refuses, which is a worse experience than saying so here.
            return Result.Failure<ReturnableSaleDto>(Error.Conflict(
                $"Invoice {sale.InvoiceNumber} was cancelled, which already restored its stock "
                + "and reversed the payment. There is nothing left to return."));
        }

        return Result.Success(sale);
    }
}
