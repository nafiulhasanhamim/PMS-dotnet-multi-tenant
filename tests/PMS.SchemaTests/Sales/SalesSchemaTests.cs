using FluentAssertions;
using Xunit;

namespace PMS.SchemaTests.Sales;

/// <summary>
/// Pins the parts of Module 5's schema that carry a business rule.
///
/// <para><b>Why these read a .sql file.</b> Constraints and indexes live in the migration
/// scripts, not in the EF model, and the schema fixture runs against an in-memory provider with
/// no indexes or CHECK constraints to inspect. Asserting the script is the only way to guard
/// the database half without a live SQL Server — and it is a real guard, because every
/// assertion below corresponds to a way the data could go quietly wrong.</para>
///
/// <para>These are deliberately about <em>invariants</em>, not about column lists. A test that
/// enumerated every column would fail on every harmless addition and teach the next person to
/// stop reading it.</para>
/// </summary>
public class SalesSchemaTests
{
    private static string ReadMigration(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null
            && !Directory.Exists(Path.Combine(directory.FullName, "database", "scripts")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull(
            "the database/scripts folder has to be findable from the test binary");

        var path = Path.Combine(directory!.FullName, "database", "scripts", fileName);

        File.Exists(path).Should().BeTrue($"{fileName} is the migration these tests are about");

        return File.ReadAllText(path);
    }

    private static string Migration => Collapse(ReadMigration("011_CreateSalesTables.sql"));

    private static string Collapse(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // ── Money precision ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CashColumns_AreTwoDecimalPlaces()
    {
        // Totals, discounts and change are cash. There is no such thing as a third of a paisa
        // of change, and storing one would let these columns stop summing to each other.
        var sql = Migration;

        sql.Should().Contain("[Subtotal] DECIMAL(18, 2)");
        sql.Should().Contain("[DiscountAmount] DECIMAL(18, 2)");
        sql.Should().Contain("[NetTotal] DECIMAL(18, 2)");
        sql.Should().Contain("[CashReceived] DECIMAL(18, 2)");
        sql.Should().Contain("[ChangeGiven] DECIMAL(18, 2)");
        sql.Should().Contain("[RefundAmount] DECIMAL(18, 2)");
    }

    [Fact]
    public void ThePriceSnapshot_IsFourDecimalPlacesLikeTheProduct()
    {
        // A price per base unit set from a pack is genuinely fractional — a strip of three at
        // ten taka. Rounding the snapshot would make a historical invoice disagree with the
        // price that was actually charged, which is the one thing the column exists to prevent.
        Migration.Should().Contain("[UnitSalePrice] DECIMAL(18, 4)");
    }

    // ── The arithmetic, enforced at rest ────────────────────────────────────────────────

    [Fact]
    public void TheNetTotal_MustEqualSubtotalLessDiscount()
    {
        // A hand-written UPDATE does not go through the domain. A sale whose columns disagree
        // with each other is one nobody can reconcile afterwards.
        Migration.Should().Contain("CK_Sales_NetIsSubtotalLessDiscount");
        Migration.Should().Contain("CHECK ([NetTotal] = [Subtotal] - [DiscountAmount])");
    }

    [Fact]
    public void TheDiscount_CannotExceedTheSubtotal()
    {
        Migration.Should().Contain("CK_Sales_DiscountWithinSubtotal");
        Migration.Should().Contain("[DiscountAmount] <= [Subtotal]");
    }

    [Fact]
    public void TheChange_MustEqualCashLessNet()
    {
        Migration.Should().Contain("CK_Sales_ChangeIsCashLessNet");
        Migration.Should().Contain("CHECK ([ChangeGiven] = [CashReceived] - [NetTotal])");
    }

    [Fact]
    public void TheCash_MustCoverTheNetTotal()
    {
        // Cash only, so a sale that does not balance cannot be recorded as a part payment.
        Migration.Should().Contain("CK_Sales_CashCoversNet");
        Migration.Should().Contain("CHECK ([CashReceived] >= [NetTotal])");
    }

    [Fact]
    public void ALineNetTotal_MustEqualItsTotalLessItsDiscountShare()
    {
        // The per-line half of the same idea, and the column a refund is computed from.
        Migration.Should().Contain("CK_SaleLines_NetIsLineLessShare");
        Migration.Should().Contain("CHECK ([NetLineTotal] = [LineTotal] - [DiscountShare])");
    }

    [Fact]
    public void ALineDiscountShare_CannotExceedTheLine()
    {
        Migration.Should().Contain("CK_SaleLines_ShareWithinLine");
        Migration.Should().Contain("[DiscountShare] <= [LineTotal]");
    }

    [Fact]
    public void ADiscount_IsBothFieldsOrNeither()
    {
        // Half a discount would leave the invoice unable to say what the customer was promised.
        Migration.Should().Contain("CK_Sales_DiscountFieldsTogether");
    }

    [Fact]
    public void ACancellation_CarriesAReasonAPersonAndATime()
    {
        // Or none of the three. A cancelled sale with no reason is the row an owner asks about
        // and cannot get an answer from.
        Migration.Should().Contain("CK_Sales_CancellationComplete");
    }

    [Fact]
    public void ASaleLine_MustMoveAtLeastOneUnit()
        => Migration.Should().Contain("CK_SaleLines_QuantityPositive");

    [Fact]
    public void AReturn_MustBeForSomethingAndCannotRefundNegatively()
    {
        Migration.Should().Contain("CK_SalesReturns_QuantityPositive");
        Migration.Should().Contain("CK_SalesReturns_RefundNotNegative");
    }

    // ── Tenancy ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheInvoiceNumber_IsUniquePerPharmacyNotGlobally()
    {
        // Two pharmacies both have an INV-000001 and neither knows about the other's. What this
        // forbids is one pharmacy issuing the same number twice, which would make a return
        // ambiguous about which sale it reverses.
        var sql = Migration;

        sql.Should().Contain("UNIQUE NONCLUSTERED INDEX [UX_Sales_Tenant_InvoiceNumber]");
        sql.Should().Contain("ON [dbo].[Sales] ([TenantId], [InvoiceNumber])");
    }

    [Fact]
    public void EverySalesTable_CarriesATenantForeignKey()
    {
        var sql = Migration;

        sql.Should().Contain("FK_Sales_Tenants");
        sql.Should().Contain("FK_SaleLines_Tenants");
        sql.Should().Contain("FK_SalesReturns_Tenants");
    }

    [Fact]
    public void TheInvoiceCounter_IsPerPharmacy()
    {
        var sql = Migration;

        sql.Should().Contain("[InvoiceSequences]");
        sql.Should().Contain("[TenantId] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_InvoiceSequences] PRIMARY KEY");
    }

    // ── The indexes Module 8 was promised ───────────────────────────────────────────────

    [Fact]
    public void SalesAreIndexedByDateAndByCashier()
    {
        // Named in the module brief. Date because every report is a date range; cashier because
        // "who sold what" is the other question, and because it is what makes an Employee's
        // own-sales-only list a seek rather than a scan of the pharmacy's sales.
        var sql = Migration;

        sql.Should().Contain("[IX_Sales_Tenant_SaleDate]");
        sql.Should().Contain("[IX_Sales_Tenant_Cashier]");
    }

    [Fact]
    public void SaleLinesAreIndexedByBatch_ForProfitReporting()
    {
        // Module 8 joins lines to batches for the purchase cost behind each sale.
        Migration.Should().Contain("[IX_SaleLines_Tenant_Batch]");
    }

    // ── Referential rules ───────────────────────────────────────────────────────────────

    [Fact]
    public void ASaleLine_CannotOutliveItsProductOrItsBatch()
    {
        // No ON DELETE CASCADE on either. Deleting a product or a batch out from under a sale
        // line would erase what was sold and where it came from — and losing the batch would
        // leave a return with nowhere to put the stock back.
        var sql = Migration;

        var products = sql.IndexOf("CONSTRAINT [FK_SaleLines_Products]", StringComparison.Ordinal);
        var batches = sql.IndexOf("CONSTRAINT [FK_SaleLines_Batches]", StringComparison.Ordinal);

        products.Should().BeGreaterThan(-1);
        batches.Should().BeGreaterThan(-1);

        // The 80 characters after each declaration cover the REFERENCES clause and any
        // ON DELETE that followed it.
        sql.Substring(products, 90).Should().NotContain("CASCADE");
        sql.Substring(batches, 90).Should().NotContain("CASCADE");
    }

    [Fact]
    public void LinesAndReturns_GoWithTheThingTheyBelongTo()
    {
        // The opposite decision, and for the opposite reason: a sale line has no meaning
        // without its sale, and a return none without its line.
        var sql = Migration;

        sql.Should().Contain("REFERENCES [dbo].[Sales] ([Id]) ON DELETE CASCADE");
        sql.Should().Contain("REFERENCES [dbo].[SaleLines] ([Id]) ON DELETE CASCADE");
    }

    // ── The run order ───────────────────────────────────────────────────────────────────

    [Fact]
    public void TheMigration_IsListedInRunAll()
    {
        // A script nobody is told to run is a schema that does not exist in the next
        // environment somebody sets up.
        var runAll = ReadMigration("000_RunAll.sql");

        runAll.Should().Contain("011_CreateSalesTables.sql");
    }

    [Fact]
    public void TheDropScript_RemovesSalesBeforeTheTablesTheyReference()
    {
        // Sale lines reference products AND batches, and returns reference sale lines, so the
        // whole billing stack has to come out first or the drop fails on a foreign key.
        var drop = Collapse(ReadMigration("099_DropAllTables.sql"));

        var returns = drop.IndexOf("[SalesReturns]", StringComparison.Ordinal);
        var lines = drop.IndexOf("[SaleLines]", StringComparison.Ordinal);
        var sales = drop.IndexOf("[Sales]", StringComparison.Ordinal);
        var batches = drop.IndexOf("[Batches]", StringComparison.Ordinal);
        var products = drop.IndexOf("[Products]", StringComparison.Ordinal);

        returns.Should().BeGreaterThan(-1);
        returns.Should().BeLessThan(lines, "a return references its sale line");
        lines.Should().BeLessThan(sales, "a line references its sale");
        lines.Should().BeLessThan(batches, "a line references its batch");
        lines.Should().BeLessThan(products, "a line references its product");
    }
}
