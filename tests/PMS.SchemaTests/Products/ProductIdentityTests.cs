using PMS.Application.Common.Products;
using FluentAssertions;
using Xunit;

namespace PMS.SchemaTests.Products;

/// <summary>
/// Pins the product identity rule on both sides of the boundary.
///
/// <para>A product is the same product when its brand name, strength <b>and dosage form</b>
/// match. The application decides that in <c>ProductKeys.Identity</c>; the database enforces
/// it with a persisted computed column and one unique index, created by migration 010.</para>
///
/// <para><b>Why this test reads a .sql file.</b> The unique index exists only in the migration
/// scripts — it is not in the EF model, and the schema fixture runs against an in-memory
/// provider that has no indexes to inspect. So the only way to assert the database half
/// without a live SQL Server is to assert the script that creates it. That is a real guard:
/// the failure it prevents is somebody narrowing the identity back to brand + strength, which
/// would silently make a pharmacy unable to stock a cream and a lotion of the same
/// medicine.</para>
/// </summary>
public class ProductIdentityTests
{
    private static string ReadMigration(string fileName)
    {
        // Walks up from the test binary to the repository root. Fragile if the layout moves,
        // and it fails loudly rather than silently passing if it does.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null
            && !Directory.Exists(Path.Combine(directory.FullName, "database", "scripts")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the database/scripts folder has to be findable from the test binary");

        var path = Path.Combine(directory!.FullName, "database", "scripts", fileName);

        File.Exists(path).Should().BeTrue($"{fileName} is the migration this test is about");

        return File.ReadAllText(path);
    }

    private static string Migration => ReadMigration("010_ProductIdentityIncludesDosageForm.sql");

    private static string Collapse(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // ── The database half ───────────────────────────────────────────────────────────────

    [Fact]
    public void TheComputedIdentityColumn_ConcatenatesAllThreeParts()
    {
        var sql = Collapse(Migration);

        sql.Should().Contain("[IdentityKey]");
        sql.Should().Contain("[BrandName]");
        sql.Should().Contain("ISNULL([Strength]");
        sql.Should().Contain("ISNULL([DosageForm]",
            "dosage form is part of a product's identity — that is what migration 010 is for");
        sql.Should().Contain("PERSISTED",
            "a unique index needs the computed value materialised");
    }

    [Fact]
    public void TheIdentityIndex_IsUniquePerTenant()
    {
        var sql = Collapse(Migration);

        sql.Should().Contain("CREATE UNIQUE NONCLUSTERED INDEX [UX_Products_Tenant_Identity]");
        sql.Should().Contain("ON [dbo].[Products] ([TenantId], [IdentityKey])",
            "uniqueness is per pharmacy: two pharmacies may both stock Napa 500");
    }

    [Fact]
    public void TheOldBrandAndStrengthIndexes_AreDropped()
    {
        var sql = Collapse(Migration);

        sql.Should().Contain("DROP INDEX [UX_Products_Tenant_Brand_Strength]");
        sql.Should().Contain("DROP INDEX [UX_Products_Tenant_Brand_NoStrength]",
            "leaving the narrower unique index in place would keep refusing the second dosage "
            + "form, so widening the identity would have no effect");
    }

    [Fact]
    public void TheReplacementIsCreatedBeforeTheOldOnesAreDropped()
    {
        var sql = Collapse(Migration);

        var created = sql.IndexOf("CREATE UNIQUE NONCLUSTERED INDEX [UX_Products_Tenant_Identity]",
            StringComparison.Ordinal);
        var dropped = sql.IndexOf("DROP INDEX [UX_Products_Tenant_Brand_Strength]",
            StringComparison.Ordinal);

        created.Should().BeLessThan(dropped,
            "dropping first would leave the table with no uniqueness guarantee at all, "
            + "however briefly");
    }

    [Fact]
    public void TheBrandNameLookupIndex_SurvivesTheDrop()
    {
        // The dropped unique index was also what made a brand-name search seekable. Losing it
        // silently would turn every product search into a scan.
        Collapse(Migration).Should()
            .Contain("CREATE NONCLUSTERED INDEX [IX_Products_Tenant_Brand_Strength]");
    }

    // ── The two halves agree ────────────────────────────────────────────────────────────

    [Fact]
    public void TheApplicationKey_UsesTheSameSeparatorAsTheColumn()
    {
        // CHAR(31) in SQL, '' in C#. If these drift, the in-memory duplicate check and
        // the constraint disagree about what collides.
        Collapse(Migration).Should().Contain("CHAR(31)");

        var key = ProductKeys.Identity("Napa", "500 mg", "Tablet");

        key.Should().Be("napa500 mgtablet");
    }

    [Fact]
    public void TheApplicationKey_TreatsAbsentPartsAsEmpty_LikeIsNull()
    {
        ProductKeys.Identity("Hartmann Solution", null, null)
            .Should().Be("hartmann solution");
    }
}
