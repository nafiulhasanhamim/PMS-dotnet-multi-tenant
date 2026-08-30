using PMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PMS.Persistence.Configurations;

/// <summary>
/// Entity configuration for AccessLog.
/// </summary>
public class AccessLogConfiguration : IEntityTypeConfiguration<AccessLog>
{
    public void Configure(EntityTypeBuilder<AccessLog> builder)
    {
        builder.ToTable("AccessLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .HasMaxLength(450);

        builder.Property(x => x.UserEmail)
            .HasMaxLength(256);

        builder.Property(x => x.UserRole)
            .HasMaxLength(100);

        builder.Property(x => x.EntityName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.EntityId)
            .HasMaxLength(100);

        builder.Property(x => x.Action)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.RequestUrl)
            .HasMaxLength(2000);

        builder.Property(x => x.HttpMethod)
            .HasMaxLength(10);

        builder.Property(x => x.IpAddress)
            .HasMaxLength(45); // IPv6 max length

        builder.Property(x => x.UserAgent)
            .HasMaxLength(500);

        builder.Property(x => x.AdditionalInfo)
            .HasMaxLength(4000);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(4000);

        // Indexes for common queries
        builder.HasIndex(x => x.AccessDateUtc);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.EntityName);
        builder.HasIndex(x => new { x.EntityName, x.EntityId });
        builder.HasIndex(x => x.Action);
    }
}
