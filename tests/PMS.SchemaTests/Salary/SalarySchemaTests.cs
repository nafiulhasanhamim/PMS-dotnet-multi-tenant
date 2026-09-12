using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PMS.Domain.Entities;
using PMS.SchemaTests.Common;
using Xunit;

namespace PMS.SchemaTests.Salary;

/// <summary>
/// Pins the parts of Module 9's schema that carry a business rule.
///
/// <para><b>Why these read a .sql file.</b> Constraints and filtered indexes live in the
/// migration scripts, not in the EF model, and the schema fixture runs against an in-memory
/// provider with no indexes or CHECK constraints to inspect. Asserting the script is the only way
/// to guard the database half without a live SQL Server &mdash; and every assertion below
/// corresponds to a way the payroll could go quietly wrong.</para>
///
/// <para>Deliberately about <em>invariants</em>, not column lists. A test that enumerated every
/// column would fail on every harmless addition and teach the next person to stop reading it.</para>
/// </summary>
public class SalarySchemaTests : IClassFixture<SchemaTestFixture>
{
    private readonly SchemaTestFixture _fixture;

    public SalarySchemaTests(SchemaTestFixture fixture) => _fixture = fixture;

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

    private static string Migration => Collapse(ReadMigration("015_CreateSalaryTables.sql"));

    private static string Collapse(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // ── Money and dates ─────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryMoneyColumn_IsTwoDecimalPlaces()
    {
        // A salary, a bonus and an advance are all cash that changed hands as a whole amount.
        // Unlike purchasing there is no per-base-unit price here, so nothing warrants 18,4.
        var sql = Migration;

        sql.Should().Contain("[MonthlyBaseSalary] DECIMAL(18, 2)");
        sql.Should().Contain("[BaseSalary] DECIMAL(18, 2)");
        sql.Should().Contain("[Bonus] DECIMAL(18, 2)");
        sql.Should().Contain("[AdvanceDeduction] DECIMAL(18, 2)");
        sql.Should().Contain("[OtherDeduction] DECIMAL(18, 2)");
        sql.Should().Contain("[NetPayable] DECIMAL(18, 2)");
        sql.Should().Contain("[Amount] DECIMAL(18, 2)");
    }

    [Fact]
    public void MoneyMovingDates_AreDateNotDateTime()
    {
        // A salary is paid on a day and an advance is handed over on a day. A time component
        // would invite a report to slice on it and silently drop everything stamped 00:00 from
        // a range beginning at 09:00.
        var sql = Migration;

        sql.Should().Contain("[PaymentDate] DATE");
        sql.Should().Contain("[AdvanceDate] DATE");
        sql.Should().Contain("[JoiningDate] DATE");
    }

    [Fact]
    public void RowVersion_IsNullableVarbinary_NotSqlRowversion()
    {
        // Matches byte[]? on the aggregate root, and matches every other table in this folder.
        // A real ROWVERSION column is generated and cannot be written, which breaks the mapping.
        Migration.Should().Contain("[RowVersion] VARBINARY(MAX) NULL");
        Migration.Should().NotContain("ROWVERSION NOT NULL");
    }

    // ── The constraints that stop a payroll going wrong ─────────────────────────────────

    [Fact]
    public void AnEmployeesMonth_CanOnlyBeGeneratedOnce()
    {
        // The constraint that makes pressing "generate for August" twice safe: the second press
        // is refused by the database, not merely by a handler that happened to look first.
        var sql = Migration;

        sql.Should().Contain("CREATE UNIQUE INDEX [UX_SalaryEntries_Tenant_Profile_Period]");
        sql.Should().Contain(
            "ON [dbo].[SalaryEntries] ([TenantId], [EmployeeSalaryProfileId], [Year], [Month])");
    }

    [Fact]
    public void OneActiveProfilePerUser_IsUniqueButFiltered()
    {
        // Filtered, so somebody who leaves and rejoins gets a second profile while the first
        // keeps its history. An unfiltered index would make rejoining impossible without editing
        // the old row, which is the one thing that table must never do.
        var sql = Migration;

        sql.Should().Contain(
            "CREATE UNIQUE INDEX [UX_EmployeeSalaryProfiles_Tenant_User_Active]");
        sql.Should().Contain("ON [dbo].[EmployeeSalaryProfiles] ([TenantId], [UserId]) WHERE [IsActive] = 1");
    }

    [Fact]
    public void NetPayable_CannotBeNegative()
    {
        // ">= 0" rather than an equality against the four components, because the arithmetic is
        // FLOORED at zero and a floor is not an equation: when advances exceed what the month can
        // pay, net payable is 0 while the components sum below it.
        var sql = Migration;

        sql.Should().Contain("CONSTRAINT [CK_SalaryEntries_NetPayableNotNegative] CHECK ([NetPayable] >= 0)");
        sql.Should().NotContain("[NetPayable] = [BaseSalary]");
    }

    [Fact]
    public void APaidEntry_MustCarryAPaymentDate()
    {
        // Either half alone is a row no screen can render honestly - a paid salary nobody can
        // date, or a payment date on money that has not moved. And the date is what the expense
        // report keys on.
        Migration.Should().Contain("CONSTRAINT [CK_SalaryEntries_PaidHasDate]");
        Migration.Should().Contain(
            "([PaymentStatus] = 0 AND [PaymentDate] IS NULL) OR ([PaymentStatus] = 1 AND [PaymentDate] IS NOT NULL)");
    }

    [Fact]
    public void ASettledAdvance_MustNameTheEntryThatSettledIt()
    {
        // Without this the pair can drift, and "how much of Karim's advance is still outstanding"
        // stops being answerable.
        Migration.Should().Contain("CONSTRAINT [CK_SalaryAdvances_SettledHasEntry]");
        Migration.Should().Contain(
            "([IsSettled] = 0 AND [SettledInSalaryEntryId] IS NULL) OR ([IsSettled] = 1 AND [SettledInSalaryEntryId] IS NOT NULL)");
    }

    [Fact]
    public void AnAdvance_MustBePositive()
    {
        // Money going the other way is a salary, not an advance.
        Migration.Should().Contain("CONSTRAINT [CK_SalaryAdvances_AmountPositive] CHECK ([Amount] > 0)");
    }

    [Fact]
    public void MonthAndYear_AreBounded()
    {
        var sql = Migration;

        sql.Should().Contain("CONSTRAINT [CK_SalaryEntries_Month] CHECK ([Month] BETWEEN 1 AND 12)");
        sql.Should().Contain("CONSTRAINT [CK_SalaryEntries_Year] CHECK ([Year] BETWEEN 2000 AND 2200)");
    }

    [Fact]
    public void NothingCascades_BecauseDeactivationIsASoftDelete()
    {
        // Every salary entry and every advance points at a profile. A cascade would mean
        // deleting somebody's record took a year of payslips with it - the record of money that
        // left the till.
        Migration.Should().NotContain("ON DELETE CASCADE");
        Migration.Should().NotContain("ON DELETE SET NULL");
    }

    // ── The indexes the expense report depends on ───────────────────────────────────────

    [Fact]
    public void TheOperatingExpenseQueries_HaveAnIndexEach()
    {
        // Paid entries by payment date, and advances by advance date. Both are read on every
        // monthly report, and both are the shape IOperatingExpenses sums over.
        var sql = Migration;

        sql.Should().Contain("[IX_SalaryEntries_Tenant_PaymentDate]");
        sql.Should().Contain("ON [dbo].[SalaryEntries] ([TenantId], [PaymentStatus], [PaymentDate])");

        sql.Should().Contain("[IX_SalaryAdvances_Tenant_AdvanceDate]");
        sql.Should().Contain("ON [dbo].[SalaryAdvances] ([TenantId], [AdvanceDate])");
    }

    // ── The EF model half ───────────────────────────────────────────────────────────────

    [Fact]
    public void AllThreeEntities_AreMapped()
    {
        _fixture.Model.FindEntityType(typeof(EmployeeSalaryProfile)).Should().NotBeNull();
        _fixture.Model.FindEntityType(typeof(SalaryEntry)).Should().NotBeNull();
        _fixture.Model.FindEntityType(typeof(SalaryAdvance)).Should().NotBeNull();
    }

    [Fact]
    public void AllThreeEntities_AreTenantFiltered()
    {
        // By convention rather than by a HasQueryFilter in the configuration - declaring one
        // there would REPLACE the convention's rather than combine with it, which is the trap
        // Module 2 documented. This asserts the convention actually reached them.
        foreach (var type in new[]
                 {
                     typeof(EmployeeSalaryProfile), typeof(SalaryEntry), typeof(SalaryAdvance),
                 })
        {
            _fixture.Model.FindEntityType(type)!.GetQueryFilter()
                .Should().NotBeNull($"{type.Name} is tenant-scoped");
        }
    }

    [Fact]
    public void NewAdvanceTranches_IsNotMappedAsANavigation()
    {
        // It is a hand-off to the caller, not a relationship. Mapped, it would become a second
        // SalaryAdvance collection on SalaryEntry with a shadow foreign key sitting beside the
        // real SettledInSalaryEntryId - and rows would be written through the wrong one.
        var entry = _fixture.Model.FindEntityType(typeof(SalaryEntry))!;

        entry.GetNavigations()
            .Should().NotContain(n => n.Name == nameof(SalaryEntry.NewAdvanceTranches));

        entry.FindProperty(nameof(SalaryEntry.NewAdvanceTranches)).Should().BeNull();
    }
}
