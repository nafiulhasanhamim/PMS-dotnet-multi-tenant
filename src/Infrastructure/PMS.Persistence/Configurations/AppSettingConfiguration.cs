using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Persistence.Configurations;

/// <summary>
/// Module 10: one configurable value per pharmacy.
///
/// <para>No <c>HasQueryFilter</c> here. <c>AppSetting</c> implements <c>ITenantEntity</c>, so
/// <c>ApplicationDbContext</c>'s convention pass adds the tenant filter and
/// <c>TenantEntityInterceptor</c> stamps <c>TenantId</c> on insert. A filter written here would
/// <em>replace</em> the convention's rather than combine with it — the trap Module 2
/// documented.</para>
///
/// <para>That automatic filter is what makes two pharmacies' discount caps independent without a
/// single call site passing a tenant id.</para>
/// </summary>
public sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("AppSettings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Key).IsRequired().HasMaxLength(100);

        // Non-nullable with an empty default: a blank drug licence number is "no value", not
        // "unknown", and removing that distinction removes the question of what null meant.
        builder.Property(s => s.Value).IsRequired().HasMaxLength(1000);

        builder.Property(s => s.UpdatedByUserId);

        // Unique per pharmacy, with the value included so both the single-key read and the
        // whole-set read are answered from the index alone. Settings are read on nearly every
        // request - billing wants the caps, the invoice wants the pharmacy details.
        builder.HasIndex(s => new { s.TenantId, s.Key })
            .IsUnique()
            .HasDatabaseName("UX_AppSettings_Tenant_Key");
    }
}
