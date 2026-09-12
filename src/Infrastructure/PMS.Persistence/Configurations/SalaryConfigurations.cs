using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Persistence.Configurations;

// ═══════════════════════════════════════════════════════════════════════════════════════════
// Module 9: employee salary profiles, monthly salary entries and salary advances.
//
// None of these declares HasQueryFilter. All three implement ITenantEntity, so
// ApplicationDbContext's convention pass adds the tenant filter and TenantEntityInterceptor
// stamps TenantId on insert. A filter written here would REPLACE the convention's rather than
// combine with it — the trap Module 2 documented.
//
// Every money column is DECIMAL(18,2). Unlike purchasing there is no per-base-unit price here:
// a salary, a bonus and an advance are all cash that changed hands as a whole amount.
//
// Both dates are 'date'. A salary is paid on a day and an advance is handed over on a day; a
// time component would invite a report to slice on it and silently drop everything stamped
// 00:00 from a range that begins at 09:00.
// ═══════════════════════════════════════════════════════════════════════════════════════════

public sealed class EmployeeSalaryProfileConfiguration
    : IEntityTypeConfiguration<EmployeeSalaryProfile>
{
    public void Configure(EntityTypeBuilder<EmployeeSalaryProfile> builder)
    {
        builder.ToTable("EmployeeSalaryProfiles");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.UserId).IsRequired();
        builder.Property(p => p.Designation).HasMaxLength(200);
        builder.Property(p => p.MonthlyBaseSalary).IsRequired().HasPrecision(18, 2);
        builder.Property(p => p.JoiningDate).IsRequired().HasColumnType("date");
        builder.Property(p => p.IsActive).IsRequired();

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            // Restrict. A user who has ever been paid cannot be deleted out from under their
            // own payslips.
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered unique: one ACTIVE profile per user per pharmacy. Somebody who leaves and
        // rejoins gets a second profile while the first keeps its history — which an unfiltered
        // index would make impossible without editing the old row, the one thing this table must
        // never do.
        builder.HasIndex(p => new { p.TenantId, p.UserId })
            .IsUnique()
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("UX_EmployeeSalaryProfiles_Tenant_User_Active");

        // Every screen in the module starts from "the active payroll".
        builder.HasIndex(p => new { p.TenantId, p.IsActive })
            .HasDatabaseName("IX_EmployeeSalaryProfiles_Tenant_Active");
    }
}

public sealed class SalaryEntryConfiguration : IEntityTypeConfiguration<SalaryEntry>
{
    public void Configure(EntityTypeBuilder<SalaryEntry> builder)
    {
        builder.ToTable("SalaryEntries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Month).IsRequired();
        builder.Property(e => e.Year).IsRequired();

        builder.Property(e => e.BaseSalary).IsRequired().HasPrecision(18, 2);
        builder.Property(e => e.Bonus).IsRequired().HasPrecision(18, 2);
        builder.Property(e => e.AdvanceDeduction).IsRequired().HasPrecision(18, 2);
        builder.Property(e => e.OtherDeduction).IsRequired().HasPrecision(18, 2);
        builder.Property(e => e.NetPayable).IsRequired().HasPrecision(18, 2);

        builder.Property(e => e.AdjustmentNotes).HasMaxLength(1000);

        // Stored as its underlying int, like every other enum in the system. The API serialises
        // enums as integers too, so the wire, the model and the column all agree.
        builder.Property(e => e.PaymentStatus).IsRequired().HasConversion<int>();

        builder.Property(e => e.PaymentDate).HasColumnType("date");
        builder.Property(e => e.GeneratedByUserId).IsRequired();

        // NOT a navigation, and EF would otherwise map it as one - a collection of SalaryAdvance
        // hanging off SalaryEntry, with a shadow foreign key and a second relationship beside the
        // real SettledInSalaryEntryId. It is a hand-off: advance tranches that settlement created
        // and the handler must persist. See SalaryEntry.NewAdvanceTranches.
        builder.Ignore(e => e.NewAdvanceTranches);

        builder.HasOne(e => e.EmployeeSalaryProfile)
            .WithMany()
            .HasForeignKey(e => e.EmployeeSalaryProfileId)
            // Restrict, matching the profile's soft-delete rule. Deactivating somebody must
            // never take a year of payslips with it.
            .OnDelete(DeleteBehavior.Restrict);

        // An employee's month can only exist once, which is what makes pressing "generate for
        // August" twice safe: the second attempt is refused by the database, not merely by a
        // handler that happened to look first.
        builder.HasIndex(e => new { e.TenantId, e.EmployeeSalaryProfileId, e.Year, e.Month })
            .IsUnique()
            .HasDatabaseName("UX_SalaryEntries_Tenant_Profile_Period");

        // The month listing.
        builder.HasIndex(e => new { e.TenantId, e.Year, e.Month })
            .HasDatabaseName("IX_SalaryEntries_Tenant_Period");

        // The operating-expense query: paid entries whose PaymentDate falls in a range. The
        // INCLUDE is in the migration; EF has no expression for it, and the index is useful
        // here either way.
        builder.HasIndex(e => new { e.TenantId, e.PaymentStatus, e.PaymentDate })
            .HasDatabaseName("IX_SalaryEntries_Tenant_PaymentDate");
    }
}

public sealed class SalaryAdvanceConfiguration : IEntityTypeConfiguration<SalaryAdvance>
{
    public void Configure(EntityTypeBuilder<SalaryAdvance> builder)
    {
        builder.ToTable("SalaryAdvances");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Amount).IsRequired().HasPrecision(18, 2);
        builder.Property(a => a.AdvanceDate).IsRequired().HasColumnType("date");
        builder.Property(a => a.Reason).HasMaxLength(500);
        builder.Property(a => a.GivenByUserId).IsRequired();
        builder.Property(a => a.IsSettled).IsRequired();

        builder.HasOne(a => a.EmployeeSalaryProfile)
            .WithMany()
            .HasForeignKey(a => a.EmployeeSalaryProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.SettledInSalaryEntry)
            .WithMany()
            .HasForeignKey(a => a.SettledInSalaryEntryId)
            // Restrict rather than SetNull. There is no delete-entry endpoint, and this is part
            // of why: blanking the link silently would leave IsSettled = 1 pointing at nothing,
            // and the money would never be recovered. Anything that ever does delete an entry
            // has to release its advances first, and this refuses to let it skip that.
            .OnDelete(DeleteBehavior.Restrict);

        // Generation's question: what is still unsettled for this employee, oldest first.
        builder.HasIndex(a => new
            {
                a.TenantId,
                a.EmployeeSalaryProfileId,
                a.IsSettled,
                a.AdvanceDate,
            })
            .HasDatabaseName("IX_SalaryAdvances_Tenant_Profile_Settled");

        // The operating-expense query: advances given within a date range, settled or not.
        // An advance is an expense on the day the cash left, independent of the salary that
        // eventually nets it out.
        builder.HasIndex(a => new { a.TenantId, a.AdvanceDate })
            .HasDatabaseName("IX_SalaryAdvances_Tenant_AdvanceDate");

        // Revising or deleting an unpaid entry reads its settled advances by entry id.
        builder.HasIndex(a => new { a.TenantId, a.SettledInSalaryEntryId })
            .HasDatabaseName("IX_SalaryAdvances_Tenant_SettledEntry");
    }
}
