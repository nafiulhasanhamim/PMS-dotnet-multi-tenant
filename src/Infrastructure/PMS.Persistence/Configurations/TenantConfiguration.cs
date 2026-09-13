using PMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PMS.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);

        // 253 is the longest a fully-qualified domain name can be (RFC 1035).
        builder.Property(t => t.DomainName).IsRequired().HasMaxLength(253);

        builder.Property(t => t.Status).IsRequired().HasConversion<int>();
        builder.Property(t => t.SubscriptionPlan).HasMaxLength(100);
        // Module 7. Stored as an int, defaulting to Off - see the enum and migration 012 for
        // why the default is the loosest setting rather than the strictest.

        builder.Property(t => t.IsDeleted).IsRequired();
        builder.Property(t => t.DeletedBy).HasMaxLength(256);

        // A domain decides which pharmacy a login targets, so it must resolve to exactly
        // one. Filtered on IsDeleted so a domain can be reused after a pharmacy is removed.
        builder.HasIndex(t => t.DomainName)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
