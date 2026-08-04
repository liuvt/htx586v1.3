using HTX586CONTRACT.Domain.Offices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HTX586CONTRACT.Infrastructure.Persistence.Configurations;

public sealed class AdminOfficeConfiguration : IEntityTypeConfiguration<AdminOffice>
{
    public void Configure(EntityTypeBuilder<AdminOffice> builder)
    {
        builder.ToTable("AdminOffices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AdminUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.AssignedByUserId).HasMaxLength(450);
        builder.Property(x => x.AssignedAt).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.AdminUser)
            .WithMany(x => x.AdminOffices)
            .HasForeignKey(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CompanyProfile)
            .WithMany(x => x.AdminOffices)
            .HasForeignKey(x => x.CompanyProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.AdminUserId, x.CompanyProfileId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_AdminOffices_Admin_Office");
        builder.HasIndex(x => new { x.CompanyProfileId, x.IsActive })
            .HasDatabaseName("IX_AdminOffices_Office_Active");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
