using Ardalis.Specification;
using PMS.Domain.Entities;

namespace PMS.Application.Features.Purchases.Specifications;

/// <summary>
/// One purchase line, with everything a return needs to decide and to write.
///
/// <para><b>The returns are the reason this include list is not shorter.</b> The returnable
/// quantity is capped by what has already gone back, and loading the line without its returns
/// would make <c>PurchaseLine.ReturnedInBaseUnits</c> read zero — quietly, with no error — and let
/// the same goods be sent back twice. <c>SaleWithLinesSpec</c> carries the identical warning for
/// the identical reason.</para>
///
/// <para>The batch and its product come too: the batch is adjusted, and the product is what
/// converts the entered unit into base units and formats the answer.</para>
/// </summary>
public sealed class PurchaseLineForReturnSpec : SingleResultSpecification<PurchaseLine>
{
    public PurchaseLineForReturnSpec(Guid purchaseId, Guid purchaseLineId)
    {
        Query
            .Where(line => line.Id == purchaseLineId && line.PurchaseId == purchaseId)
            .Include(line => line.Returns)
            .Include(line => line.Batch)
            .ThenInclude(batch => batch.Product)
            .Include(line => line.Purchase);
    }
}
