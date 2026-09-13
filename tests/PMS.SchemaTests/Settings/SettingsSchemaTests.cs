using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Settings;
using PMS.Domain.Entities;
using PMS.SchemaTests.Common;
using Xunit;

namespace PMS.SchemaTests.Settings;

/// <summary>
/// Pins the parts of Module 10's schema and catalogue that carry a business rule.
///
/// <para><b>Why these read a .sql file.</b> Constraints and indexes live in the migration
/// scripts, not in the EF model, and the schema fixture runs against an in-memory provider with
/// no indexes or CHECK constraints to inspect. Asserting the script is the only way to guard the
/// database half without a live SQL Server.</para>
///
/// <para><b>And why several read <see cref="SettingKeys"/>.</b> Key-value storage buys a cheap
/// new setting at the cost of the database type-checking nothing. That cost is only acceptable
/// because the catalogue is the single declaration — so the catalogue is what these check.</para>
/// </summary>
public class SettingsSchemaTests : IClassFixture<SchemaTestFixture>
{
    private readonly SchemaTestFixture _fixture;

    public SettingsSchemaTests(SchemaTestFixture fixture) => _fixture = fixture;

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

    private static string Collapse(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Migration => Collapse(ReadMigration("016_CreateAppSettingsTable.sql"));

    // ── The table ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void OneRowPerKeyPerPharmacy()
    {
        // The constraint that makes "which value is in force" a question with one answer, and
        // that makes the seed safe to re-run.
        var sql = Migration;

        sql.Should().Contain("CREATE UNIQUE INDEX [UX_AppSettings_Tenant_Key]");
        sql.Should().Contain("ON [dbo].[AppSettings] ([TenantId], [Key])");
    }

    [Fact]
    public void TheValueColumn_IsNotNullable()
    {
        // Empty rather than NULL for a blank setting. A drug licence number the pharmacy does
        // not have is "no value", not "unknown", and removing the distinction removes the
        // question of which one an empty string meant.
        Migration.Should().Contain("[Value] NVARCHAR(1000) NOT NULL");
    }

    [Fact]
    public void UpdatedByUserId_HasNoForeignKey()
    {
        // Deliberate: a setting outlives the person who set it. Restrict would block removing a
        // user who once saved this page; Cascade would take the setting with them.
        Migration.Should().NotContain("FK_AppSettings_Users");
    }

    [Fact]
    public void RowVersion_IsNullableVarbinary_NotSqlRowversion()
    {
        Migration.Should().Contain("[RowVersion] VARBINARY(MAX) NULL");
        Migration.Should().NotContain("ROWVERSION NOT NULL");
    }

    // ── The antibiotic mode's move ──────────────────────────────────────────────────────

    [Fact]
    public void TheAntibioticMode_IsCopiedBeforeTheColumnIsDropped()
    {
        // The guard is not ceremonial. Dropping the column first and failing the insert
        // afterwards would lose the regulatory setting of every pharmacy running under Required.
        var sql = Migration;

        var copy = sql.IndexOf("Migrated antibiotic_prescription_mode", StringComparison.Ordinal);
        var drop = sql.IndexOf("DROP COLUMN [AntibioticPrescriptionMode]", StringComparison.Ordinal);

        copy.Should().BeGreaterThan(0, "the migration has to copy the value");
        drop.Should().BeGreaterThan(copy, "and it has to copy before it drops");
    }

    [Fact]
    public void TheDrop_IsGuardedOnEveryPharmacyHavingTheSetting()
    {
        Migration.Should().Contain("DROP COLUMN [AntibioticPrescriptionMode]");
        Migration.Should().Contain("WHERE NOT EXISTS");
    }

    [Fact]
    public void ExistingValues_AreCopiedRatherThanDefaulted()
    {
        // A pharmacy running under Required keeps running under Required across the upgrade.
        // Silently relaxing a regulatory setting would be the worst thing this script could do.
        var sql = Migration;

        sql.Should().Contain("WHEN 1 THEN N''Optional''");
        sql.Should().Contain("WHEN 2 THEN N''Required''");
    }

    [Fact]
    public void TheSeed_NeverOverwritesAnExistingValue()
    {
        // Every insert in this script is guarded, so re-running it cannot undo a change an Admin
        // has made since. That is what makes it safe to leave in a run-all script forever.
        //
        // Counted rather than searched for a keyword: the script's own comments discuss the
        // alternatives it rejected, so "does the word MERGE appear" is a question about the prose
        // rather than about the SQL.
        var sql = Migration;

        var inserts = Occurrences(sql, "INSERT INTO [dbo].[AppSettings]")
                      + Occurrences(sql, "INSERT INTO [dbo].[AppSettings]".Replace("[dbo].", ""));

        var guards = Occurrences(sql, "WHERE NOT EXISTS ( SELECT 1 FROM [dbo].[AppSettings]")
                     + Occurrences(sql, "WHERE NOT EXISTS ( SELECT 1 FROM [dbo].[AppSettings] AS s");

        inserts.Should().BeGreaterThan(0, "the migration has to seed something");

        guards.Should().BeGreaterThanOrEqualTo(
            inserts, "every insert into AppSettings must be guarded by a NOT EXISTS");

        // And nothing rewrites a row that is already there.
        sql.Should().NotContain("UPDATE [dbo].[AppSettings]");
        sql.Should().NotContain("DELETE FROM [dbo].[AppSettings]");
    }

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        var at = haystack.IndexOf(needle, StringComparison.Ordinal);

        while (at >= 0)
        {
            count++;
            at = haystack.IndexOf(needle, at + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }

    // ── The catalogue is the single declaration ─────────────────────────────────────────

    [Fact]
    public void EveryDeclaredKey_IsSeededByTheMigration()
    {
        // The failure this prevents: adding a key to SettingKeys, shipping, and every existing
        // pharmacy silently running on the fallback with a warning per read.
        var sql = Migration;

        foreach (var definition in SettingKeys.All)
        {
            sql.Should().Contain(
                definition.Key,
                $"migration 016 has to seed '{definition.Key}' for existing pharmacies");
        }
    }

    [Fact]
    public void EveryKey_IsDeclaredExactlyOnce()
        => SettingKeys.All.Select(d => d.Key).Should().OnlyHaveUniqueItems();

    [Fact]
    public void EveryDefault_ParsesAsItsOwnKind()
    {
        // The defaults ARE the fallbacks, so a mistyped one fails silently: GetIntAsync would
        // fall back to a default that does not parse and return zero, which for an expiry window
        // means the alert quietly reports nothing.
        foreach (var definition in SettingKeys.All)
        {
            switch (definition.Kind)
            {
                case SettingKind.Integer:
                    int.TryParse(definition.Default, out var number)
                        .Should().BeTrue($"'{definition.Key}' defaults to an integer");

                    number.Should().BeInRange(
                        definition.Minimum, definition.Maximum,
                        $"'{definition.Key}' must default to something it would itself accept");
                    break;

                case SettingKind.Enumeration:
                    definition.EnumType.Should().NotBeNull();

                    Enum.GetNames(definition.EnumType!)
                        .Should().Contain(definition.Default);
                    break;

                case SettingKind.Text:
                    if (definition.Required)
                    {
                        definition.Default.Should().NotBeNullOrWhiteSpace(
                            $"'{definition.Key}' is required, so its fallback cannot be blank");
                    }

                    break;
            }
        }
    }

    [Fact]
    public void EveryKey_IsLowerSnakeCase()
    {
        // These strings appear in the database, in migration 016 and in the API's JSON alike. A
        // convention that survives all three unchanged is worth more than matching C# here - and
        // a stray capital would be a key the seed and the reader disagreed about.
        foreach (var definition in SettingKeys.All)
        {
            definition.Key.Should().MatchRegex("^[a-z][a-z0-9_]*$");
        }
    }

    [Fact]
    public void EveryKey_IsShortEnoughForTheColumn()
        => SettingKeys.All.Should().OnlyContain(d => d.Key.Length <= 100);

    // ── The EF model half ───────────────────────────────────────────────────────────────

    [Fact]
    public void AppSetting_IsMappedAndTenantFiltered()
    {
        var entity = _fixture.Model.FindEntityType(typeof(AppSetting));

        entity.Should().NotBeNull();

        // By convention rather than a HasQueryFilter in the configuration - declaring one there
        // would REPLACE the convention's rather than combine with it. This is what makes two
        // pharmacies' discount caps independent without a call site passing a tenant id.
        entity!.GetQueryFilter().Should().NotBeNull();
    }

    [Fact]
    public void TheTenantRow_NoLongerCarriesTheAntibioticMode()
    {
        // One source of truth. Two places to store one value is exactly the drift Module 10 was
        // written to remove - a settings screen writing one column while billing reads another
        // is a bug nobody finds until an inspection.
        var tenant = _fixture.Model.FindEntityType(typeof(Tenant));

        tenant.Should().NotBeNull();
        tenant!.FindProperty("AntibioticPrescriptionMode").Should().BeNull();
    }
}
