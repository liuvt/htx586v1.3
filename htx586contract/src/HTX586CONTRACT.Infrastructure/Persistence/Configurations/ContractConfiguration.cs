using HTX586CONTRACT.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HTX586CONTRACT.Infrastructure.Persistence.Configurations;

public sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("Contracts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ContractNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.AdminId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.DriverId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.CompanyNameSnapshot).HasMaxLength(300).IsRequired();
        builder.Property(x => x.DriverNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CustomerNameSnapshot).HasMaxLength(300).IsRequired();
        builder.Property(x => x.CustomerPhoneSnapshot).HasMaxLength(20).IsRequired();
        builder.Property(x => x.VehiclePlateSnapshot).HasMaxLength(20);
        builder.Property(x => x.VehicleOwnerNameSnapshot).HasMaxLength(200);
        builder.Property(x => x.AreaCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.SecondDriverName).HasMaxLength(200);
        builder.Property(x => x.SecondDriverLicenseClass).HasMaxLength(20);
        builder.Property(x => x.RouteDescription).HasMaxLength(2000);
        builder.Property(x => x.TotalKilometers).HasPrecision(18, 2);
        builder.Property(x => x.PickupLocation).HasMaxLength(1000);
        builder.Property(x => x.DropoffLocation).HasMaxLength(1000);
        builder.Property(x => x.ContractValue).HasPrecision(18, 2);
        builder.Property(x => x.PaymentMethod).HasMaxLength(100);
        builder.Property(x => x.PaymentTime).HasMaxLength(200);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.ContractDataJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.ContractHash).HasMaxLength(128);
        builder.Property(x => x.PdfFileUrl).HasMaxLength(500);
        builder.Property(x => x.PdfSha256).HasMaxLength(128);
        builder.Property(x => x.DeletedBy).HasMaxLength(450);

        builder.HasOne(x => x.AdminAccount)
            .WithMany(x => x.AdminContracts)
            .HasForeignKey(x => x.AdminId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Driver)
            .WithMany(x => x.Contracts)
            .HasForeignKey(x => x.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.DriverId, x.ContractNumber })
            .IsUnique()
            .HasDatabaseName("UX_Contracts_Driver_ContractNumber");
        builder.HasIndex(x => new { x.AdminId, x.CreatedAt })
            .HasDatabaseName("IX_Contracts_Admin_CreatedAt");
        builder.HasIndex(x => new { x.DriverId, x.CreatedAt })
            .HasDatabaseName("IX_Contracts_Driver_CreatedAt");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_Contracts_Status");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
