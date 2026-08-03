using HTX586CONTRACT.Domain.Signatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HTX586CONTRACT.Infrastructure.Persistence.Configurations;

public sealed class ContractSignatureConfiguration : IEntityTypeConfiguration<ContractSignature>
{
    public void Configure(EntityTypeBuilder<ContractSignature> builder)
    {
        builder.ToTable(
            "ContractSignatures",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ContractSignatures_Latitude",
                    "[Latitude] IS NULL OR ([Latitude] >= -90 AND [Latitude] <= 90)");

                table.HasCheckConstraint(
                    "CK_ContractSignatures_Longitude",
                    "[Longitude] IS NULL OR ([Longitude] >= -180 AND [Longitude] <= 180)");
            });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SignerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.SignerPhone)
            .HasMaxLength(20);

        builder.Property(x => x.SignatureFileUrl)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.SignatureVectorJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.SignatureHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.ContractHashAtSigning)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.DeviceSignedAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(x => x.ServerSignedAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(x => x.Latitude)
            .HasPrecision(10, 7);

        builder.Property(x => x.Longitude)
            .HasPrecision(10, 7);

        builder.Property(x => x.LocationAddress)
            .HasMaxLength(500);

        builder.Property(x => x.LocationError)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.IpAddress)
            .HasMaxLength(64);

        builder.Property(x => x.DeviceId)
            .HasMaxLength(200);

        builder.Property(x => x.DeviceName)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.OperatingSystem)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.BrowserName)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.AppVersion)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ConsentText)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasOne(x => x.Contract)
            .WithMany(x => x.Signatures)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ContractId, x.Party })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ContractSignatures_Contract_Party");

        builder.HasIndex(x => x.ServerSignedAt)
            .IsDescending()
            .HasDatabaseName("IX_ContractSignatures_ServerSignedAt");

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
