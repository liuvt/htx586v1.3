using HTX586CONTRACT.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HTX586CONTRACT.Infrastructure.Persistence.Configurations;

public sealed class ContractPassengerConfiguration : IEntityTypeConfiguration<ContractPassenger>
{
    public void Configure(EntityTypeBuilder<ContractPassenger> builder)
    {
        builder.ToTable("ContractPassengers", table => table.UseSqlOutputClause(false));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Note)
            .HasMaxLength(500);

        // Dữ liệu hành khách của luồng mới nằm trong ContractDataJson.
        // Bỏ optimistic concurrency ở bảng phụ để tương thích dữ liệu cũ.
        builder.Ignore(x => x.RowVersion);

        builder.HasOne(x => x.Contract)
            .WithMany(x => x.Passengers)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ContractId, x.SortOrder })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ContractPassengers_Contract_SortOrder");

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
