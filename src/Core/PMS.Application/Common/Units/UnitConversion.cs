using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Common.Units;

/// <summary>
/// Converts between a product's selling units and the integer base-unit quantities that
/// everything downstream actually stores.
///
/// <para><b>Build this once and reuse it.</b> Batches, Billing and Reports all need the same
/// arithmetic, and three copies of it would drift. Stock is held in base units as an
/// <see cref="int"/>; nothing downstream needs to know whether that unit is a tablet, a
/// bottle or a tin.</para>
///
/// <para><b>The trap.</b> A product with a bulk pack but no middle pack stores base units per
/// large in <see cref="Product.MidPerLarge"/> — 24 bottles per carton, not 24 strips. Nothing
/// here multiplies those two fields directly; it all goes through
/// <see cref="Product.BaseUnitsPerLarge"/>, which resolves the two shapes in one place. Get
/// this wrong and a carton of 24 becomes a carton of 240, which is the kind of error that
/// only shows up as an inventory discrepancy weeks later.</para>
/// </summary>
public static class UnitConversion
{
    /// <summary>
    /// Converts a quantity expressed in <paramref name="unit"/> into base units.
    ///
    /// <para>Takes a decimal because a UI may offer "1.5 boxes", but returns an int because
    /// stock is whole base units. A quantity that does not resolve to a whole number of base
    /// units is rejected rather than rounded: silently turning 0.5 pieces into 0 or 1 loses
    /// or invents stock.</para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The product does not define <paramref name="unit"/>. That is a programming error — a
    /// UI must only offer levels the product has — so it throws rather than returning 0,
    /// which would silently record a sale of nothing.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The quantity is negative, or does not divide into whole base units.
    /// </exception>
    public static int ToBaseUnits(decimal quantity, UnitLevel unit, Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity), quantity, "A quantity cannot be negative.");
        }

        var perUnit = BaseUnitsIn(unit, product);
        var total = quantity * perUnit;

        if (total != decimal.Truncate(total))
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity), quantity,
                $"{quantity} {Describe(unit, product)} is {total} {product.BaseUnitName}, "
                + "which is not a whole number. Stock is tracked in whole base units.");
        }

        return (int)total;
    }

    /// <summary>
    /// Renders a base-unit quantity the way a person would say it, largest unit first —
    /// "1 box + 2 strips + 3 pieces", "2 cartons + 5 bottles", "30 bags".
    ///
    /// <para>Only levels the product defines appear, and a level contributing zero is
    /// omitted: 100 pieces of a 10/10 product reads "1 box", not
    /// "1 box + 0 strips + 0 pieces".</para>
    /// </summary>
    public static string FromBaseUnits(int baseUnits, Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (baseUnits == 0)
        {
            return $"0 {Pluralise(product.BaseUnitName, 0)}";
        }

        if (baseUnits < 0)
        {
            return $"-{FromBaseUnits(-baseUnits, product)}";
        }

        var parts = new List<string>();
        var remaining = baseUnits;

        var perLarge = product.BaseUnitsPerLarge;
        if (perLarge is > 0 && product.LargeUnitName is not null)
        {
            var large = Math.DivRem(remaining, perLarge.Value, out remaining);
            if (large > 0)
            {
                parts.Add($"{large} {Pluralise(product.LargeUnitName, large)}");
            }
        }

        var perMid = product.BasePerMid;
        if (perMid is > 0 && product.MidUnitName is not null)
        {
            var mid = Math.DivRem(remaining, perMid.Value, out remaining);
            if (mid > 0)
            {
                parts.Add($"{mid} {Pluralise(product.MidUnitName, mid)}");
            }
        }

        if (remaining > 0 || parts.Count == 0)
        {
            parts.Add($"{remaining} {Pluralise(product.BaseUnitName, remaining)}");
        }

        return string.Join(" + ", parts);
    }

    /// <summary>
    /// The per-base-unit price implied by a price quoted at <paramref name="unit"/>.
    ///
    /// <para>Not rounded. A carton of 24 at ৳4,320 is exactly ৳180 a bottle, but a strip of 3
    /// at ৳10 is ৳3.3333… and rounding here would quietly lose money on every line. Callers
    /// that need to display or charge a rounded figure round at that point, once.</para>
    /// </summary>
    public static decimal PricePerBaseUnit(decimal price, UnitLevel unit, Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        var perUnit = BaseUnitsIn(unit, product);

        return price / perUnit;
    }

    /// <summary>
    /// A one-line description of how a product is packed, for detail pages and the form's
    /// live summary — "1 box = 10 strips = 100 pieces", "1 carton = 24 bottles",
    /// "Sold as individual bags only".
    /// </summary>
    public static string DescribePacking(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        var basePlural = Pluralise(product.BaseUnitName, 2);

        if (!product.HasMidUnit && !product.HasLargeUnit)
        {
            return $"Sold as individual {basePlural} only";
        }

        var parts = new List<string>();

        if (product.HasLargeUnit && product.BaseUnitsPerLarge is > 0)
        {
            parts.Add($"1 {product.LargeUnitName}");

            if (product.HasMidUnit && product.MidPerLarge is > 0)
            {
                parts.Add($"{product.MidPerLarge} {Pluralise(product.MidUnitName!, product.MidPerLarge!.Value)}");
            }

            parts.Add($"{product.BaseUnitsPerLarge} {basePlural}");
        }
        else if (product.HasMidUnit && product.BasePerMid is > 0)
        {
            parts.Add($"1 {product.MidUnitName}");
            parts.Add($"{product.BasePerMid} {basePlural}");
        }

        return parts.Count > 0
            ? string.Join(" = ", parts)
            : $"Sold as individual {basePlural} only";
    }

    /// <summary>How many base units one of <paramref name="unit"/> contains.</summary>
    /// <exception cref="InvalidOperationException">The product does not define that level.</exception>
    public static int BaseUnitsIn(UnitLevel unit, Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        switch (unit)
        {
            case UnitLevel.Base:
                return 1;

            case UnitLevel.Mid:
                if (!product.HasMidUnit || product.BasePerMid is null or < 1)
                {
                    throw new InvalidOperationException(
                        $"'{product.BrandName}' has no middle unit, so a quantity cannot be "
                        + $"expressed in one. It is sold in {product.BaseUnitName}"
                        + (product.HasLargeUnit ? $" and {product.LargeUnitName}." : " only."));
                }

                return product.BasePerMid.Value;

            case UnitLevel.Large:
                var perLarge = product.BaseUnitsPerLarge;

                if (!product.HasLargeUnit || perLarge is null or < 1)
                {
                    throw new InvalidOperationException(
                        $"'{product.BrandName}' has no bulk unit, so a quantity cannot be "
                        + $"expressed in one. It is sold in {product.BaseUnitName}"
                        + (product.HasMidUnit ? $" and {product.MidUnitName}." : " only."));
                }

                return perLarge.Value;

            default:
                throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown unit level.");
        }
    }

    /// <summary>The product's own name for a level, for use in messages.</summary>
    public static string Describe(UnitLevel unit, Product product) => unit switch
    {
        UnitLevel.Base => Pluralise(product.BaseUnitName, 2),
        UnitLevel.Mid => Pluralise(product.MidUnitName ?? "mid units", 2),
        UnitLevel.Large => Pluralise(product.LargeUnitName ?? "bulk units", 2),
        _ => "units",
    };

    /// <summary>
    /// Naive English pluralisation, which is all that is needed: the unit names in use are
    /// piece, strip, box, bottle, carton, tin, bag, pack, sachet, vial, tube, ampoule.
    /// </summary>
    private static string Pluralise(string noun, int count)
    {
        if (count == 1 || string.IsNullOrWhiteSpace(noun))
        {
            return noun;
        }

        var lower = noun.ToLowerInvariant();

        if (lower.EndsWith('s') || lower.EndsWith('x') || lower.EndsWith('z')
            || lower.EndsWith("ch") || lower.EndsWith("sh"))
        {
            return noun + "es";
        }

        if (lower.Length > 1 && lower.EndsWith('y') && !"aeiou".Contains(lower[^2]))
        {
            return noun[..^1] + "ies";
        }

        return noun + "s";
    }
}
