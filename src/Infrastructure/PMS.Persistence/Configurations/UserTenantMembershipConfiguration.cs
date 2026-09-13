using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PMS.Persistence.Configurations;

public sealed class UserTenantMembershipConfiguration : IEntityTypeConfiguration<UserTenantMembership>
{
    public void Configure(EntityTypeBuilder<UserTenantMembership> builder)
    {
        builder.ToTable("UserTenantMemberships", table =>
        {
            // The rule that makes "platform admin = a membership with no pharmacy" safe.
            // Enforced by the database, not just by the factory methods, because this is the
            // invariant the whole identity model rests on: a PlatformAdmin row with a real
            // tenant would be a pharmacy user with platform powers.
            table.HasCheckConstraint(
                "CK_UserTenantMemberships_PlatformAdminHasNoTenant",
                $"([Role] = {(int)UserRole.PlatformAdmin} AND [TenantId] IS NULL) OR " +
                $"([Role] <> {(int)UserRole.PlatformAdmin} AND [TenantId] IS NOT NULL)");
        });

        builder.HasKey(m => m.Id);

        builder.Property(m => m.TenantId);
        builder.Property(m => m.UserId).IsRequired();
        builder.Property(m => m.Role).IsRequired().HasConversion<int>();
        builder.Property(m => m.IsActive).IsRequired();
        builder.Property(m => m.JoinedAt).IsRequired();

        // One membership per person per pharmacy. Filtered to real tenants, because SQL
        // Server treats NULLs as equal here and would otherwise allow only one platform
        // membership across the entire system.
        builder.HasIndex(m => new { m.TenantId, m.UserId })
            .IsUnique()
            .HasFilter("[TenantId] IS NOT NULL")
            .HasDatabaseName("UX_UserTenantMemberships_Tenant_User");

        // At most one platform membership per person.
        builder.HasIndex(m => m.UserId)
            .IsUnique()
            .HasFilter("[TenantId] IS NULL")
            .HasDatabaseName("UX_UserTenantMemberships_PlatformPerUser");

        builder.HasOne(m => m.Tenant)
            .WithMany()
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
