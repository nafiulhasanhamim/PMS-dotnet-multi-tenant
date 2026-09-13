using FluentAssertions;
using PMS.Application.Common.Billing;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Xunit;

namespace PMS.UnitTests.Application.Billing;

/// <summary>
/// Splitting one cart item across batches.
///
/// <para>The interesting cases are all edges — an exact fit, one unit short, a depleted batch
/// in the list — and testing them through the sale handler would need a database, a tenant, a
/// user and a cart for each. The allocator is a pure function over the list the FEFO helper
/// produced, so they cost nothing here.</para>
/// </summary>
public class FefoAllocatorTests
{
    private static Product Tablets()
    {
        // The constructor takes the four things every product has; levels and their prices are
        // set afterwards, because a product sold only in bags needs one price and not three.
        var product = new Product(ProductType.Medicine, "Napa 500", "piece", 1.50m);
        product.SetUnits("piece", "strip", "box", basePerMid: 10, midPerLarge: 10);
        product.SetPrices(1.50m, 15m, 140m);

        return product;
    }

    private static Batch BatchOf(Product product, string number, int quantity, DateOnly? expiry) =>
        new(
            product.Id,
            batchNumber: number,
            expiryDate: expiry,
            manufactureDate: null,
            purchasePricePerBaseUnit: 1m,
            quantityInBaseUnits: quantity,
            supplierId: null,
            supplierNameText: null,
            notes: null);

    [Fact]
    public void One_batch_with_enough_stock_produces_one_take()
    {
        var product = Tablets();
        var batch = BatchOf(product, "B-100", 100, new DateOnly(2027, 1, 1));

        var allocation = FefoAllocator.Allocate([batch], 40);

        allocation.IsSatisfied.Should().BeTrue();
        allocation.Takes.Should().HaveCount(1);
        allocation.Takes[0].QuantityInBaseUnits.Should().Be(40);
        allocation.AvailableInBaseUnits.Should().Be(100);
    }

    [Fact]
    public void The_module_brief_split_takes_the_soonest_expiry_first()
    {
        // B-101 holds 30 and expires sooner; B-100 holds 100. Selling 40 must take all 30 of
        // B-101 and 10 of B-100, in that order.
        var product = Tablets();
        var soonest = BatchOf(product, "B-101", 30, new DateOnly(2026, 6, 30));
        var later = BatchOf(product, "B-100", 100, new DateOnly(2027, 6, 30));

        var allocation = FefoAllocator.Allocate([soonest, later], 40);

        allocation.Takes.Should().HaveCount(2);
        allocation.Takes[0].Batch.BatchNumber.Should().Be("B-101");
        allocation.Takes[0].QuantityInBaseUnits.Should().Be(30);
        allocation.Takes[1].Batch.BatchNumber.Should().Be("B-100");
        allocation.Takes[1].QuantityInBaseUnits.Should().Be(10);
        allocation.Quantities.Should().Equal(30, 10);
    }

    [Fact]
    public void An_exact_fit_across_two_batches_does_not_reach_for_a_third()
    {
        var product = Tablets();
        var first = BatchOf(product, "B-1", 30, new DateOnly(2026, 6, 30));
        var second = BatchOf(product, "B-2", 10, new DateOnly(2027, 6, 30));
        var third = BatchOf(product, "B-3", 50, new DateOnly(2028, 6, 30));

        var allocation = FefoAllocator.Allocate([first, second, third], 40);

        allocation.Takes.Should().HaveCount(2);
        allocation.Takes.Should().NotContain(take => take.Batch.BatchNumber == "B-3");
    }

    [Fact]
    public void One_unit_short_across_everything_allocates_nothing_at_all()
    {
        // All or nothing. Half-selling an item and telling the cashier afterwards is worse than
        // refusing, because the stock has already moved.
        var product = Tablets();
        var first = BatchOf(product, "B-1", 30, new DateOnly(2026, 6, 30));
        var second = BatchOf(product, "B-2", 9, new DateOnly(2027, 6, 30));

        var allocation = FefoAllocator.Allocate([first, second], 40);

        allocation.IsSatisfied.Should().BeFalse();
        allocation.Takes.Should().BeEmpty();

        // Reported even though the allocation failed: this is the number the cashier needs.
        allocation.AvailableInBaseUnits.Should().Be(39);
    }

    [Fact]
    public void No_batches_means_nothing_available()
    {
        var allocation = FefoAllocator.Allocate([], 1);

        allocation.IsSatisfied.Should().BeFalse();
        allocation.AvailableInBaseUnits.Should().Be(0);
    }

    [Fact]
    public void A_depleted_batch_in_the_list_produces_no_zero_quantity_line()
    {
        // A sold-out batch stays active and stays on the stock page - it is the cost and expiry
        // behind sales that already happened - so it can legitimately appear in an unfiltered
        // list. Skipping it keeps a zero-quantity sale line off the invoice.
        var product = Tablets();
        var empty = BatchOf(product, "B-EMPTY", 0, new DateOnly(2026, 1, 1));
        var stocked = BatchOf(product, "B-FULL", 50, new DateOnly(2027, 1, 1));

        var allocation = FefoAllocator.Allocate([empty, stocked], 10);

        allocation.Takes.Should().HaveCount(1);
        allocation.Takes[0].Batch.BatchNumber.Should().Be("B-FULL");
    }

    [Fact]
    public void A_request_for_nothing_allocates_nothing()
    {
        var product = Tablets();
        var batch = BatchOf(product, "B-1", 50, null);

        FefoAllocator.Allocate([batch], 0).IsSatisfied.Should().BeFalse();
    }

    [Fact]
    public void The_allocator_trusts_the_order_it_is_given()
    {
        // It does not re-sort. Ordering is the FEFO helper's job, and a second implementation of
        // "earliest expiry first" is exactly what that helper exists to prevent.
        var product = Tablets();
        var later = BatchOf(product, "LATER", 10, new DateOnly(2028, 1, 1));
        var sooner = BatchOf(product, "SOONER", 10, new DateOnly(2026, 1, 1));

        var allocation = FefoAllocator.Allocate([later, sooner], 15);

        allocation.Takes[0].Batch.BatchNumber.Should().Be("LATER");
    }
}
