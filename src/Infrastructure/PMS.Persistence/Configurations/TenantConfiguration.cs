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

        builder.Property(t => t.Slug).IsRequired().HasMaxLength(64);

        // The slug identifies a pharmacy in URLs and support conversations, so it has to be
        // unique across the whole system, not per tenant.
        builder.HasIndex(t => t.Slug).IsUnique().HasFilter("[IsDeleted] = 0");

        // 253 is the longest a fully-qualified domain name can be (RFC 1035).
        builder.Property(t => t.DomainName).HasMaxLength(253);

        // Filtered, and not only to skip soft-deleted rows: SQL Server treats NULLs as equal
        // in a unique index, so a plain one would permit exactly *one* tenant without a
        // domain. Excluding NULL lets any number of pharmacies have no domain while still
        // guaranteeing that a domain which is set belongs to one tenant only.
        builder.HasIndex(t => t.DomainName)
            .IsUnique()
            .HasFilter("[DomainName] IS NOT NULL AND [IsDeleted] = 0");

        builder.Property(t => t.IsActive).IsRequired();
        builder.Property(t => t.IsDeleted).IsRequired();
        builder.Property(t => t.DeletedBy).HasMaxLength(256);
    }
}
