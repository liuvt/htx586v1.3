using HTX586CONTRACT.Domain.Offices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HTX586CONTRACT.Infrastructure.Persistence.Configurations;

public sealed class OfficeVehicleConfiguration : IEntityTypeConfiguration<OfficeVehicle>
{
    public void Configure(EntityTypeBuilder<OfficeVehicle> builder)
    {
        builder.ToTable("OfficeVehicles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AssignedFrom).HasColumnType("datetime2");
        builder.Property(x => x.AssignedTo).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.Vehicle)
            .WithMany(x => x.OfficeVehicles)
            .HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CompanyProfile)
            .WithMany(x => x.OfficeVehicles)
            .HasForeignKey(x => x.CompanyProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.VehicleId)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_OfficeVehicles_Vehicle");
        builder.HasIndex(x => new { x.CompanyProfileId, x.IsActive })
            .HasDatabaseName("IX_OfficeVehicles_Office_Active");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
