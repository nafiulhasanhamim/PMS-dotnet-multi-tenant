using PMS.Domain.Entities.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PMS.Persistence.Configurations.Reporting;

/// <summary>
/// EF Core configuration for CustomerLifetimeValue entity.
/// </summary>
public class CustomerLifetimeValueConfiguration : IEntityTypeConfiguration<CustomerLifetimeValue>
{
    public void Configure(EntityTypeBuilder<CustomerLifetimeValue> builder)
    {
        builder.ToTable("CustomerLifetimeValues", "reporting");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CustomerId)
            .IsRequired();

        builder.Property(c => c.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(c => c.CustomerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.TotalSpent)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(c => c.TotalOrders)
            .IsRequired();

        builder.Property(c => c.TotalItemsPurchased)
            .IsRequired();

        builder.Property(c => c.AverageOrderValue)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.AverageDaysBetweenOrders)
            .HasPrecision(10, 2);

        builder.Property(c => c.Segment)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.RecencyScore)
            .IsRequired();

        builder.Property(c => c.FrequencyScore)
            .IsRequired();

        builder.Property(c => c.MonetaryScore)
            .IsRequired();

        builder.Property(c => c.LastCalculatedUtc)
            .IsRequired();

        // Indexes
        builder.HasIndex(c => c.CustomerId)
            .IsUnique();

        builder.HasIndex(c => c.Email);

        builder.HasIndex(c => c.Segment);

        builder.HasIndex(c => c.TotalSpent);

        builder.HasIndex(c => new { c.RecencyScore, c.FrequencyScore, c.MonetaryScore });
    }
}
