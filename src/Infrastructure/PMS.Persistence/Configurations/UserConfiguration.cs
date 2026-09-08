using PMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PMS.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(256);
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(200);
        builder.Property(u => u.IsGloballyActive).IsRequired();

        // Globally unique, not per pharmacy. Email is the identity anchor: a login supplies
        // a domain and an email, and the email alone must identify the person, so that one
        // account can hold memberships at several pharmacies.
        builder.HasIndex(u => u.Email).IsUnique();

        builder.HasMany(u => u.Memberships)
            .WithOne(m => m.User)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
