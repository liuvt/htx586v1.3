using HTX586CONTRACT.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HTX586CONTRACT.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PlateNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.VehicleCode).HasMaxLength(50);
        builder.Property(x => x.Brand).HasMaxLength(100);
        builder.Property(x => x.Model).HasMaxLength(100);
        builder.Property(x => x.VehicleType).HasMaxLength(100);
        builder.Property(x => x.Color).HasMaxLength(50);
        builder.Property(x => x.ChassisNumber).HasMaxLength(100);
        builder.Property(x => x.EngineNumber).HasMaxLength(100);

        builder.Property(x => x.OwnerName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.OwnerCitizenId).HasMaxLength(30);
        builder.Property(x => x.OwnerCitizenIdIssuedDate).HasColumnType("date");
        builder.Property(x => x.OwnerCitizenIdIssuedPlace).HasMaxLength(300);
        builder.Property(x => x.OwnerAddress).HasMaxLength(500);
        builder.Property(x => x.OwnerPhoneNumber).HasMaxLength(20);

        builder.Property(x => x.OwnerSignatureFileUrl).HasMaxLength(500);
        builder.Property(x => x.OwnerSignatureHash).HasMaxLength(128);
        builder.Property(x => x.OwnerSignedAt).HasColumnType("datetime2");

        builder.Property(x => x.AccountDriverSignatureFileUrl).HasMaxLength(500);
        builder.Property(x => x.AccountDriverSignatureHash).HasMaxLength(128);
        builder.Property(x => x.AccountDriverSignedAt).HasColumnType("datetime2");
        builder.Property(x => x.AccountDriverSignedByUserId).HasMaxLength(450);

        builder.Property(x => x.AssignedDriverId).HasMaxLength(450);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.CompanyProfile)
            .WithMany(x => x.Vehicles)
            .HasForeignKey(x => x.CompanyProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AssignedDriver)
            .WithMany()
            .HasForeignKey(x => x.AssignedDriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PlateNumber)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_Vehicles_PlateNumber");

        builder.HasIndex(x => x.CompanyProfileId)
            .HasDatabaseName("IX_Vehicles_CompanyProfileId");

        // Một VehicleOwner có nhiều xe. Index này chỉ phục vụ truy vấn, không unique.
        builder.HasIndex(x => x.AssignedDriverId)
            .HasDatabaseName("IX_Vehicles_AssignedVehicleOwnerId");

        builder.HasIndex(x => new { x.AssignedDriverId, x.CompanyProfileId, x.IsActive })
            .HasDatabaseName("IX_Vehicles_VehicleOwner_Company_Active");

        builder.HasIndex(x => x.IsActive).HasDatabaseName("IX_Vehicles_IsActive");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
