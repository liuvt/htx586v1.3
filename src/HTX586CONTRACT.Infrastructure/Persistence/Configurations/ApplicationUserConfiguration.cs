using HTX586CONTRACT.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HTX586CONTRACT.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("AspNetUsers", table => table.UseSqlOutputClause(false));

        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RegistrationStatus).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RegistrationViewedByUserId).HasMaxLength(450);
        builder.Property(x => x.RegistrationReviewedByUserId).HasMaxLength(450);
        builder.Property(x => x.RegistrationReviewNote).HasMaxLength(1000);
        builder.Property(x => x.EmployeeCode).HasMaxLength(30);
        builder.Property(x => x.CitizenId).HasMaxLength(30);
        builder.Property(x => x.CitizenIdIssuedDate).HasColumnType("date");
        builder.Property(x => x.CitizenIdIssuedPlace).HasMaxLength(300);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.AreaCode).HasMaxLength(20);
        builder.Property(x => x.AvatarUrl).HasMaxLength(500);
        builder.Property(x => x.CitizenIdFrontUrl).HasMaxLength(500);
        builder.Property(x => x.CitizenIdBackUrl).HasMaxLength(500);
        builder.Property(x => x.DriverLicenseNumber).HasMaxLength(50);
        builder.Property(x => x.DriverLicenseClass).HasMaxLength(20);
        builder.Property(x => x.DriverLicenseIssuedDate).HasColumnType("date");
        builder.Property(x => x.DriverLicenseExpiryDate).HasColumnType("date");
        builder.Property(x => x.DriverLicenseFrontUrl).HasMaxLength(500);
        builder.Property(x => x.DriverLicenseBackUrl).HasMaxLength(500);
        builder.Property(x => x.DriverSignatureFileUrl).HasMaxLength(500);
        builder.Property(x => x.DriverSignatureHash).HasMaxLength(128);
        builder.Property(x => x.DriverSignedAt).HasColumnType("datetime2");

        builder.Property(x => x.AdminId).HasMaxLength(450);
        builder.HasOne(x => x.AdminAccount)
            .WithMany(x => x.ManagedDrivers)
            .HasForeignKey(x => x.AdminId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.AdminId).HasDatabaseName("IX_AspNetUsers_AdminId");

        builder.Property(x => x.CompanyName).HasMaxLength(300);
        builder.Property(x => x.CompanyBranchName).HasMaxLength(300);
        builder.Property(x => x.CompanyTaxCode).HasMaxLength(50);
        builder.Property(x => x.CompanyBusinessLicenseNumber).HasMaxLength(100);
        builder.Property(x => x.CompanyAddress).HasMaxLength(500);
        builder.Property(x => x.CompanyPhoneNumber).HasMaxLength(20);
        builder.Property(x => x.CompanyEmail).HasMaxLength(256);
        builder.Property(x => x.CompanyRepresentativeName).HasMaxLength(200);
        builder.Property(x => x.CompanyRepresentativePosition).HasMaxLength(100);
        builder.Property(x => x.CompanyRepresentativeCitizenId).HasMaxLength(30);
        builder.Property(x => x.CompanyRepresentativeCitizenIdIssuedDate).HasColumnType("date");
        builder.Property(x => x.CompanyRepresentativeCitizenIdIssuedPlace).HasMaxLength(300);
        builder.Property(x => x.CompanySignatureFileUrl).HasMaxLength(500);
        builder.Property(x => x.CompanySignatureHash).HasMaxLength(128);
        builder.Property(x => x.CompanySignedAt).HasColumnType("datetime2");

        // Quan hệ cũ chỉ dùng cho dữ liệu lịch sử.
        builder.HasOne(x => x.CompanyProfile)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.CompanyProfileId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CompanyProfileId).HasDatabaseName("IX_AspNetUsers_CompanyProfileId");

        builder.HasIndex(x => x.EmployeeCode)
            .IsUnique()
            .HasFilter("[EmployeeCode] IS NOT NULL")
            .HasDatabaseName("UX_AspNetUsers_EmployeeCode");
        builder.HasIndex(x => x.CitizenId)
            .HasFilter("[CitizenId] IS NOT NULL")
            .HasDatabaseName("IX_AspNetUsers_CitizenId");
        builder.Property(x => x.DeletedBy).HasMaxLength(450);
        builder.HasIndex(x => x.IsActive).HasDatabaseName("IX_AspNetUsers_IsActive");
        builder.HasIndex(x => x.IsDeleted).HasDatabaseName("IX_AspNetUsers_IsDeleted");
    }
}
