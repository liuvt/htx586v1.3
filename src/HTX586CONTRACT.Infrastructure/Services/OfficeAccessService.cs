using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Infrastructure.Services;

public sealed class OfficeAccessService(IDbContextFactory<ApplicationDbContext> factory) : IOfficeAccessService
{
    public async Task<HashSet<Guid>> GetManagedOfficeIdsAsync(
        string userId,
        bool isOwner,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (isOwner)
        {
            return (await db.CompanyProfiles.AsNoTracking()
                .Where(x => x.IsActive && !x.IsDeleted)
                .Select(x => x.Id)
                .ToListAsync(ct)).ToHashSet();
        }

        return (await db.AdminOffices.AsNoTracking()
            .Where(x => x.AdminUserId == userId && x.IsActive && !x.IsDeleted &&
                        x.CompanyProfile.IsActive && !x.CompanyProfile.IsDeleted)
            .Select(x => x.CompanyProfileId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();
    }

    public async Task<bool> CanManageOfficeAsync(
        string userId,
        Guid officeId,
        bool isOwner,
        CancellationToken ct = default)
    {
        if (isOwner)
            return true;
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AdminOffices.AsNoTracking().AnyAsync(x =>
            x.AdminUserId == userId && x.CompanyProfileId == officeId && x.IsActive && !x.IsDeleted &&
            x.CompanyProfile.IsActive && !x.CompanyProfile.IsDeleted, ct);
    }

    public async Task<bool> CanManageVehicleAsync(
        string userId,
        Guid vehicleId,
        bool isOwner,
        CancellationToken ct = default)
    {
        if (isOwner)
            return true;
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.OfficeVehicles.AsNoTracking().AnyAsync(x =>
            x.VehicleId == vehicleId &&
            x.Vehicle.IsActive && !x.Vehicle.IsDeleted &&
            x.IsActive && !x.IsDeleted && x.AssignedTo == null &&
            x.CompanyProfile.IsActive && !x.CompanyProfile.IsDeleted &&
            db.AdminOffices.Any(a =>
                a.AdminUserId == userId &&
                a.CompanyProfileId == x.CompanyProfileId &&
                a.IsActive && !a.IsDeleted &&
                a.CompanyProfile.IsActive && !a.CompanyProfile.IsDeleted), ct);
    }
}
