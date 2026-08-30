using PMS.Domain.Entities;
using PMS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PMS.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Customer entity.
/// </summary>
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        // Primary Key
        builder.HasKey(c => c.Id);

        // Properties
        builder.Property(c => c.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.LastName)
            .HasMaxLength(100)
            .IsRequired();

        // Email value object - owned type stored as column
        builder.OwnsOne(c => c.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("Email")
                .HasMaxLength(Email.MaxLength)
                .IsRequired();

            email.HasIndex(e => e.Value)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        // PhoneNumber value object - owned type stored as column (nullable)
        builder.OwnsOne(c => c.PhoneNumber, phone =>
        {
            phone.Property(p => p.Value)
                .HasColumnName("PhoneNumber")
                .HasMaxLength(20);
        });

        // ShippingAddress value object - owned type stored as columns (nullable)
        builder.OwnsOne(c => c.ShippingAddress, address =>
        {
            address.Property(a => a.Street)
                .HasColumnName("ShippingStreet")
                .HasMaxLength(200);

            address.Property(a => a.City)
                .HasColumnName("ShippingCity")
                .HasMaxLength(100);

            address.Property(a => a.State)
                .HasColumnName("ShippingState")
                .HasMaxLength(100);

            address.Property(a => a.PostalCode)
                .HasColumnName("ShippingPostalCode")
                .HasMaxLength(20);

            address.Property(a => a.Country)
                .HasColumnName("ShippingCountry")
                .HasMaxLength(100);
        });

        // Status enum stored as string
        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Audit fields
        builder.Property(c => c.CreatedOnUtc)
            .IsRequired();

        builder.Property(c => c.CreatedBy)
            .HasMaxLength(256);

        builder.Property(c => c.ModifiedBy)
            .HasMaxLength(256);

        // Soft delete fields
        builder.Property(c => c.DeletedBy)
            .HasMaxLength(256);

        // Navigation - Orders
        builder.HasMany(c => c.Orders)
            .WithOne(o => o.Customer)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(c => c.LastName);
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.IsDeleted);

        // Concurrency
        builder.Property(c => c.RowVersion)
            .IsRowVersion();
    }
}
