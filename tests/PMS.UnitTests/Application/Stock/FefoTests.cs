using PMS.Application.Common.Stock;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace PMS.UnitTests.Application.Stock;

/// <summary>
/// The FEFO ordering rules.
///
/// <para>These are the tests the billing module will depend on without knowing it. Selling
/// from the wrong batch does not throw, does not look wrong on any screen, and shows up weeks
/// later as stock that expired on the shelf while newer stock was sold — so the rules are
/// pinned here rather than left to be re-derived at the point of sale.</para>
///
/// <para>They exercise the in-memory overloads. The <see cref="IQueryable{T}"/> overloads that
/// EF translates are built from the same expression objects — see <see cref="Fefo"/> — so a
/// change that broke one would break these too.</para>
/// </summary>
public class FefoTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);

    private static readonly Guid ProductId = Guid.NewGuid();

    /// <summary>
    /// A product to hang batches off. Its unit shape is irrelevant to ordering, which is the
    /// point: FEFO knows nothing about pharmaceutical units.
    /// </summary>
    private static Product Product() =>
        new(ProductType.Medicine, "Napa 500", "piece", 1.20m);

    private static Batch Batch(
        string number,
        DateOnly? expiry,
        int quantity = 100,
        int createdDaysAgo = 30,
        bool active = true)
    {
        var batch = new Batch(
            ProductId, number, expiry, manufactureDate: null,
            purchasePricePerBaseUnit: 0.80m, quantityInBaseUnits: quantity,
            supplierId: null, supplierNameText: null, notes: null);

        // Set by the audit interceptor in production. The tiebreaker reads it, so a test that
        // left every batch at default(DateTime) would tie on the tiebreaker as well and prove
        // nothing about it.
        batch.CreatedOnUtc = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc)
            .AddDays(-createdDaysAgo);

        if (!active)
        {
            batch.Deactivate();
        }

        return batch;
    }

    /// <summary>Depletes a batch the only way anything can: through an adjustment.</summary>
    private static Batch Depleted(string number, DateOnly? expiry)
    {
        var batch = Batch(number, expiry, quantity: 100);

        batch.Adjust(AdjustmentType.Remove, -100, "sold out", Guid.NewGuid());

        return batch;
    }

    // ── 1. Several expiries, in order ───────────────────────────────────────────────────

    [Fact]
    public void GetActiveBatchesFefo_OrdersByExpiryAscending()
    {
        var batches = new[]
        {
            Batch("LATE", Today.AddDays(300)),
            Batch("SOON", Today.AddDays(10)),
            Batch("MIDDLE", Today.AddDays(120)),
        };

        var ordered = Fefo.GetActiveBatchesFefo(batches, Today).ToList();

        ordered.Select(b => b.BatchNumber).Should().Equal("SOON", "MIDDLE", "LATE");
    }

    // ── 2. Depleted batches are not sellable ────────────────────────────────────────────

    [Fact]
    public void GetActiveBatchesFefo_ExcludesDepletedBatches()
    {
        var batches = new[]
        {
            Depleted("EMPTY", Today.AddDays(5)),
            Batch("HAS-STOCK", Today.AddDays(200)),
        };

        var ordered = Fefo.GetActiveBatchesFefo(batches, Today).ToList();

        // The depleted batch expires soonest, so an ordering that ignored quantity would put
        // it first and Module 5 would try to take stock from nothing.
        ordered.Select(b => b.BatchNumber).Should().Equal("HAS-STOCK");
    }

    // ── 3. Expired batches are not sellable ─────────────────────────────────────────────

    [Fact]
    public void GetActiveBatchesFefo_ExcludesExpiredBatches()
    {
        var batches = new[]
        {
            Batch("EXPIRED", Today.AddDays(-1)),
            Batch("GOOD", Today.AddDays(90)),
        };

        var ordered = Fefo.GetActiveBatchesFefo(batches, Today).ToList();

        ordered.Select(b => b.BatchNumber).Should().Equal("GOOD");
    }

    [Fact]
    public void GetActiveBatchesFefo_IncludesABatchExpiringToday()
    {
        // The boundary, and it belongs on the sellable side: stock is good until the end of
        // the day printed on the pack, and excluding it would write off a day of stock every
        // time. An off-by-one here is a real cost.
        var batches = new[] { Batch("TODAY", Today) };

        var ordered = Fefo.GetActiveBatchesFefo(batches, Today).ToList();

        ordered.Select(b => b.BatchNumber).Should().Equal("TODAY");
    }

    // ── 4. A null expiry is sellable, and goes last ─────────────────────────────────────

    [Fact]
    public void GetActiveBatchesFefo_IncludesBatchesWithNoExpiry()
    {
        var batches = new[] { Batch("NO-EXPIRY", expiry: null) };

        var ordered = Fefo.GetActiveBatchesFefo(batches, Today).ToList();

        ordered.Select(b => b.BatchNumber).Should().Equal("NO-EXPIRY");
    }

    // ── 5. Mixed: dated first in date order, nulls after ────────────────────────────────

    [Fact]
    public void GetActiveBatchesFefo_SortsNullExpiriesLast()
    {
        var batches = new[]
        {
            Batch("NO-EXPIRY-A", expiry: null, createdDaysAgo: 90),
            Batch("FAR", Today.AddDays(3000)),
            Batch("NO-EXPIRY-B", expiry: null, createdDaysAgo: 10),
            Batch("NEAR", Today.AddDays(1)),
        };

        var ordered = Fefo.GetActiveBatchesFefo(batches, Today).ToList();

        // The rule that both LINQ and SQL Server get backwards on their own: an ascending sort
        // puts NULL first in both. Stock with a deadline has to be sold before stock without
        // one, however distant that deadline is — FAR expires in eight years and still comes
        // before a batch that never expires.
        ordered.Select(b => b.BatchNumber)
            .Should().Equal("NEAR", "FAR", "NO-EXPIRY-A", "NO-EXPIRY-B");
    }

    // ── 6. Nothing sellable ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetActiveBatchesFefo_ReturnsEmptyWhenNothingIsSellable()
    {
        var batches = new[]
        {
            Depleted("EMPTY", Today.AddDays(30)),
            Batch("EXPIRED", Today.AddDays(-30)),
            Batch("HIDDEN", Today.AddDays(30), active: false),
        };

        var ordered = Fefo.GetActiveBatchesFefo(batches, Today).ToList();

        ordered.Should().BeEmpty();
    }

    [Fact]
    public void GetActiveBatchesFefo_ReturnsEmptyForNoBatchesAtAll()
        => Fefo.GetActiveBatchesFefo([], Today).Should().BeEmpty();

    // ── 7. The tiebreaker ───────────────────────────────────────────────────────────────

    [Fact]
    public void GetActiveBatchesFefo_BreaksAnExpiryTieByOldestFirst()
    {
        var sameExpiry = Today.AddDays(60);

        var batches = new[]
        {
            Batch("NEWER", sameExpiry, createdDaysAgo: 5),
            Batch("OLDER", sameExpiry, createdDaysAgo: 50),
        };

        var ordered = Fefo.GetActiveBatchesFefo(batches, Today).ToList();

        // Two deliveries genuinely can share an expiry date. Without the tiebreaker the order
        // between them is whatever the query plan produced — stable enough to pass a test once
        // and to vary in production. Oldest delivery first, which is what a pharmacist does by
        // hand anyway.
        ordered.Select(b => b.BatchNumber).Should().Equal("OLDER", "NEWER");
    }

    [Fact]
    public void GetActiveBatchesFefo_IsDeterministicRegardlessOfInputOrder()
    {
        var sameExpiry = Today.AddDays(60);

        var forwards = new[]
        {
            Batch("A", sameExpiry, createdDaysAgo: 30),
            Batch("B", sameExpiry, createdDaysAgo: 20),
            Batch("C", sameExpiry, createdDaysAgo: 10),
        };

        var backwards = forwards.Reverse().ToArray();

        Fefo.GetActiveBatchesFefo(forwards, Today).Select(b => b.BatchNumber)
            .Should().Equal(
                Fefo.GetActiveBatchesFefo(backwards, Today).Select(b => b.BatchNumber));
    }

    // ── Inactive batches ────────────────────────────────────────────────────────────────

    [Fact]
    public void GetActiveBatchesFefo_ExcludesInactiveBatches()
    {
        // IsActive is for a row entered in error, and such a row is not stock. Note this is
        // not depletion: a sold-out batch stays active and stays on the detail page.
        var batches = new[]
        {
            Batch("ENTERED-IN-ERROR", Today.AddDays(1), active: false),
            Batch("REAL", Today.AddDays(100)),
        };

        var ordered = Fefo.GetActiveBatchesFefo(batches, Today).ToList();

        ordered.Select(b => b.BatchNumber).Should().Equal("REAL");
    }

    // ── Ordering is separable from filtering, which the detail page relies on ────────────

    [Fact]
    public void InFefoOrder_WithoutTheSellableFilter_KeepsExpiredBatchesAtTheTop()
    {
        var batches = new[]
        {
            Batch("GOOD", Today.AddDays(100)),
            Batch("EXPIRED", Today.AddDays(-10)),
        };

        var ordered = batches.InFefoOrder().ToList();

        // The product stock page lists every batch that still holds stock, expired ones
        // included, in this order — so the most urgent row is the top one, in red. That page
        // needs the ordering without the filter, which is why the two are separate methods.
        ordered.Select(b => b.BatchNumber).Should().Equal("EXPIRED", "GOOD");
    }
}
