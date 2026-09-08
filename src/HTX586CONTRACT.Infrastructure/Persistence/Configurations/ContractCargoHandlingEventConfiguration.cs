using HTX586CONTRACT.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HTX586CONTRACT.Infrastructure.Persistence.Configurations;

public sealed class ContractCargoHandlingEventConfiguration : IEntityTypeConfiguration<ContractCargoHandlingEvent>
{
    public void Configure(EntityTypeBuilder<ContractCargoHandlingEvent> builder)
    {
        builder.ToTable("ContractCargoHandlingEvents");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Location).HasMaxLength(1000);
        builder.Property(x => x.CargoWeight).HasMaxLength(100);
        builder.Property(x => x.EventTime).HasColumnType("datetime2");
        builder.Property(x => x.Confirmation).HasMaxLength(500);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.Contract)
            .WithMany(x => x.CargoHandlingEvents)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ContractId, x.Type, x.SortOrder })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ContractCargoHandlingEvents_Contract_Type_SortOrder");

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
