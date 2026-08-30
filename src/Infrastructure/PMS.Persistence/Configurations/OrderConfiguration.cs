using PMS.Domain.Entities;
using PMS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PMS.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Order entity.
/// </summary>
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        // Primary Key
        builder.HasKey(o => o.Id);

        // Properties
        builder.Property(o => o.OrderNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.CustomerId)
            .IsRequired();

        builder.Property(o => o.OrderDateUtc)
            .IsRequired();

        // Status enum stored as string
        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.Notes)
            .HasMaxLength(2000);

        // ShippingAddress value object - owned type stored as columns
        builder.OwnsOne(o => o.ShippingAddress, address =>
        {
            address.Property(a => a.Street)
                .HasColumnName("ShippingStreet")
                .HasMaxLength(200)
                .IsRequired();

            address.Property(a => a.City)
                .HasColumnName("ShippingCity")
                .HasMaxLength(100)
                .IsRequired();

            address.Property(a => a.State)
                .HasColumnName("ShippingState")
                .HasMaxLength(100)
                .IsRequired();

            address.Property(a => a.PostalCode)
                .HasColumnName("ShippingPostalCode")
                .HasMaxLength(20)
                .IsRequired();

            address.Property(a => a.Country)
                .HasColumnName("ShippingCountry")
                .HasMaxLength(100)
                .IsRequired();
        });

        // Audit fields
        builder.Property(o => o.CreatedOnUtc)
            .IsRequired();

        builder.Property(o => o.CreatedBy)
            .HasMaxLength(256);

        builder.Property(o => o.ModifiedBy)
            .HasMaxLength(256);

        // Soft delete fields
        builder.Property(o => o.DeletedBy)
            .HasMaxLength(256);

        // Computed columns are ignored - Total and Subtotal are calculated properties
        builder.Ignore(o => o.Total);
        builder.Ignore(o => o.Subtotal);

        // Navigation - Customer
        builder.HasOne(o => o.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Navigation - Items
        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(o => o.OrderNumber)
            .IsUnique();

        builder.HasIndex(o => o.CustomerId);
        builder.HasIndex(o => o.OrderDateUtc);
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.IsDeleted);
    }
}
