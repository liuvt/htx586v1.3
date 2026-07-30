using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Domain.Vehicles;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Contract> Contracts => Set<Contract>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ConvertDeletesToSoftDeletes();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ConvertDeletesToSoftDeletes();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ConvertDeletesToSoftDeletes()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries()
                     .Where(x => x.State == EntityState.Deleted && x.Entity is ISoftDeletable))
        {
            entry.State = EntityState.Modified;
            var entity = (ISoftDeletable)entry.Entity;
            entity.IsDeleted = true;
            entity.DeletedAt ??= now;
            entity.DeletedBy ??= "APPLICATION";

            if (entry.Entity is ApplicationUser user)
            {
                user.IsActive = false;
                user.SecurityStamp = Guid.NewGuid().ToString("N");
                user.UpdatedAt = now;
            }
        }
    }
}
