using PMS.Application.Common.DTOs;
using PMS.Application.Common.Units;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Common.Stock;

/// <summary>
/// Entity-to-DTO mapping for stock, and the one place the derived figures are worked out.
///
/// <para>Quantities are formatted here rather than by each client because the phrasing comes
/// from the product's own unit configuration and Module 2's converter — "142 pieces" reads as
/// "1 box + 4 strips + 2 pieces" only if you know the packing. A web page could do it; a
/// mobile client would then do it slightly differently, and a report differently again.</para>
///
/// <para>Expiry state is decided here for the same reason, and one more: a later alerts module
/// will need "is this expiring soon" without rendering anything, and it must get the same
/// answer as the amber cell on the stock list.</para>
/// </summary>
public static class StockMapping
{
    public static BatchDto ToDto(
        Batch batch,
        Product product,
        bool includePurchasePrice,
        DateOnly today,
        int expiringSoonWindowDays = StockPolicy.ExpiringSoonWindowDays)
    {
        var daysUntilExpiry = batch.DaysUntilExpiry(today);

        return new BatchDto(
            batch.Id,
            batch.ProductId,
            product.BrandName,
            batch.BatchNumber,
            batch.ExpiryDate,
            batch.ManufactureDate,
            daysUntilExpiry,
            StateOf(daysUntilExpiry, expiringSoonWindowDays),

            // Withheld from an Employee by not being read, rather than by being blanked after
            // the fact. The caller decides from the token's role; see the controller.
            includePurchasePrice ? batch.PurchasePricePerBaseUnit : null,

            batch.QuantityInBaseUnits,
            UnitConversion.FromBaseUnits(batch.QuantityInBaseUnits, product),
            batch.InitialQuantityInBaseUnits,
            UnitConversion.FromBaseUnits(batch.InitialQuantityInBaseUnits, product),
            batch.SupplierId,
            batch.SupplierNameText,
            batch.Notes,
            batch.IsActive,
            batch.CreatedOnUtc,
            batch.ModifiedOnUtc);
    }

    /// <summary>
    /// Where a batch stands against its expiry date.
    ///
    /// <para>Note the order of the tests: expired is checked before expiring-soon, because a
    /// batch thirty days past its date is also "within ninety days" and calling that amber
    /// rather than red would be the single most consequential mislabel on the screen.</para>
    /// </summary>
    public static ExpiryState StateOf(int? daysUntilExpiry, int expiringSoonWindowDays)
        => daysUntilExpiry switch
        {
            null => ExpiryState.NotApplicable,
            < 0 => ExpiryState.Expired,
            var days when days <= expiringSoonWindowDays => ExpiryState.ExpiringSoon,
            _ => ExpiryState.Ok,
        };

    /// <summary>
    /// A product's stock status against its reorder level.
    ///
    /// <para>Zero is out of stock even when the reorder level is zero, which is why this is
    /// not simply a comparison: a reorder level of 0 means "do not warn me", not "an empty
    /// shelf is fine".</para>
    /// </summary>
    public static StockStatus StatusOf(int totalInBaseUnits, int reorderLevel)
        => totalInBaseUnits <= 0
            ? StockStatus.OutOfStock
            : totalInBaseUnits <= reorderLevel
                ? StockStatus.Low
                : StockStatus.Ok;

    /// <summary>
    /// A signed quantity change, phrased in the product's units — "−20 pieces", "+2 strips".
    ///
    /// <para>The sign is carried explicitly with a real minus sign rather than left to the
    /// number's own formatting, because a history table where additions and removals are told
    /// apart only by a hyphen is one a person misreads.</para>
    /// </summary>
    public static string FormatChange(int changeInBaseUnits, Product product)
    {
        if (changeInBaseUnits == 0)
        {
            return $"no change";
        }

        var magnitude = UnitConversion.FromBaseUnits(Math.Abs(changeInBaseUnits), product);

        return changeInBaseUnits > 0 ? $"+{magnitude}" : $"−{magnitude}";
    }
}
