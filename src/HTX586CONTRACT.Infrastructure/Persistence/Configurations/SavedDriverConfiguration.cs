using HTX586CONTRACT.Domain.Drivers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HTX586CONTRACT.Infrastructure.Persistence.Configurations;

public sealed class SavedDriverConfiguration : IEntityTypeConfiguration<SavedDriver>
{
    public void Configure(EntityTypeBuilder<SavedDriver> builder)
    {
        builder.ToTable("SavedDrivers");
        builder.HasKey(x => x.DriverLicenseNumber);

        builder.Property(x => x.DriverLicenseNumber)
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(x => x.FullName)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(x => x.DriverLicenseClass)
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(20);
        builder.Property(x => x.CreatedByUserId)
            .HasMaxLength(450)
            .IsRequired();
        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.CreatedByUserId, x.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_SavedDrivers_CreatedBy_CreatedAt");
        builder.HasIndex(x => new { x.CreatedByUserId, x.IsDeleted })
            .HasDatabaseName("IX_SavedDrivers_CreatedBy_IsDeleted");

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
