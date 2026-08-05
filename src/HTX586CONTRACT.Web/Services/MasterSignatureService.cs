using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Web.Services;

/// <summary>
/// Quản lý chữ ký master của Công ty/Văn phòng, tài khoản Chủ xe và chữ ký
/// tài xế theo xe. Các chữ ký master được chụp snapshot theo từng hợp đồng.
/// </summary>
public sealed class MasterSignatureService(
    IDbContextFactory<ApplicationDbContext> factory,
    IUploadFileStorage storage)
{
    public async Task<string> SaveCompanyRepresentativeSignatureAsync(
        Guid companyId,
        string dataUrl,
        CancellationToken ct = default)
    {
        var stored = await storage.SavePngDataUrlAsync(
            ["master-signatures", "companies", companyId.ToString("N")],
            "representative",
            dataUrl,
            ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        var updated = await db.CompanyProfiles
            .Where(x => x.Id == companyId && !x.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RepresentativeSignatureFileUrl, stored.RelativeUrl)
                .SetProperty(x => x.RepresentativeSignatureHash, stored.Sha256Hash)
                .SetProperty(x => x.RepresentativeSignedAt, stored.SavedAt)
                .SetProperty(x => x.UpdatedAt, stored.SavedAt),
                ct);

        if (updated != 1)
            throw new KeyNotFoundException("Không tìm thấy công ty/văn phòng đại diện để lưu chữ ký.");

        return stored.RelativeUrl;
    }


    /// <summary>
    /// Lưu chân ký pháp lý dùng chung của tài khoản Chủ xe. Chữ ký được đồng bộ
    /// sang tất cả xe đang thuộc tài khoản để các màn hình cũ và HĐ mới cùng dùng
    /// một nguồn dữ liệu. HĐ đã tạo vẫn giữ ảnh snapshot tại thời điểm lập.
    /// </summary>
    public async Task<string> SaveVehicleOwnerAccountSignatureAsync(
        string userId,
        string dataUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("Thiếu tài khoản Chủ xe.");

        await using (var checkDb = await factory.CreateDbContextAsync(ct))
        {
            var isVehicleOwner = await (
                from ur in checkDb.UserRoles.AsNoTracking()
                join role in checkDb.Roles.AsNoTracking() on ur.RoleId equals role.Id
                where ur.UserId == userId && role.Name == "VehicleOwner"
                select ur.UserId).AnyAsync(ct);

            var isValidUser = await checkDb.Users.AsNoTracking()
                .AnyAsync(x => x.Id == userId && !x.IsDeleted, ct);

            if (!isValidUser || !isVehicleOwner)
                throw new KeyNotFoundException("Không tìm thấy tài khoản Chủ xe để lưu chân ký.");
        }

        var stored = await storage.SavePngDataUrlAsync(
            ["master-signatures", "vehicle-owners", userId],
            "owner",
            dataUrl,
            ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var updatedUser = await db.Users
            .Where(x => x.Id == userId && !x.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.VehicleOwnerSignatureFileUrl, stored.RelativeUrl)
                .SetProperty(x => x.VehicleOwnerSignatureHash, stored.Sha256Hash)
                .SetProperty(x => x.VehicleOwnerSignedAt, stored.SavedAt)
                .SetProperty(x => x.UpdatedAt, stored.SavedAt),
                ct);

        if (updatedUser != 1)
            throw new KeyNotFoundException("Không tìm thấy tài khoản Chủ xe để lưu chân ký.");

        await db.Vehicles
            .Where(x => x.AssignedDriverId == userId && !x.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.OwnerSignatureFileUrl, stored.RelativeUrl)
                .SetProperty(x => x.OwnerSignatureHash, stored.Sha256Hash)
                .SetProperty(x => x.OwnerSignedAt, stored.SavedAt)
                .SetProperty(x => x.UpdatedAt, stored.SavedAt),
                ct);

        await transaction.CommitAsync(ct);
        return stored.RelativeUrl;
    }

    public async Task<string> SaveDriverInitialSignatureAsync(string userId, string dataUrl, CancellationToken ct = default)
    {
        await using (var checkDb = await factory.CreateDbContextAsync(ct))
        {
            var alreadySigned = await checkDb.Users.AsNoTracking().AnyAsync(x => x.Id == userId && !x.IsDeleted && x.DriverSignedAt != null, ct);
            if (alreadySigned) throw new InvalidOperationException("Tài xế đã tạo chữ ký lần đầu. Không thể ký lại.");
        }
        return await SaveDriverSignatureAsync(userId, dataUrl, ct);
    }

    public async Task<string> SaveDriverSignatureAsync(
        string userId,
        string dataUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("Thiếu tài khoản tài xế.");

        var stored = await storage.SavePngDataUrlAsync(
            ["master-signatures", "drivers", userId],
            "driver",
            dataUrl,
            ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        var updated = await db.Users
            .Where(x => x.Id == userId && !x.IsDeleted
                && db.UserRoles.Any(ur => ur.UserId == x.Id
                    && db.Roles.Any(role => role.Id == ur.RoleId && role.Name == "VehicleOwner")))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.DriverSignatureFileUrl, stored.RelativeUrl)
                .SetProperty(x => x.DriverSignatureHash, stored.Sha256Hash)
                .SetProperty(x => x.DriverSignedAt, stored.SavedAt)
                .SetProperty(x => x.DriverSignatureIsActive, true)
                .SetProperty(x => x.DriverSignatureInactiveAt, (DateTime?)null)
                .SetProperty(x => x.UpdatedAt, stored.SavedAt),
                ct);

        if (updated != 1)
            throw new KeyNotFoundException("Không tìm thấy tài xế để lưu chữ ký cố định.");

        return stored.RelativeUrl;
    }
    public async Task<string> SaveVehicleAccountDriverSignatureAsync(
        Guid vehicleId,
        string userId,
        string dataUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("Không xác định được tài khoản Chủ xe.");

        await using (var checkDb = await factory.CreateDbContextAsync(ct))
        {
            var vehicle = await checkDb.Vehicles.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == vehicleId && x.IsActive, ct)
                ?? throw new KeyNotFoundException("Không tìm thấy xe đang hoạt động.");

            if (!string.Equals(vehicle.AssignedDriverId, userId, StringComparison.Ordinal))
                throw new InvalidOperationException("Chỉ tài khoản Chủ xe đang được gán xe mới được tạo chữ ký cho xe này.");
            if (vehicle.AccountDriverSignedAt.HasValue || !string.IsNullOrWhiteSpace(vehicle.AccountDriverSignatureFileUrl))
                throw new InvalidOperationException("Chữ ký tài xế cho xe này đã được xác nhận và không thể tự thay đổi lần hai.");
        }

        var stored = await storage.SavePngDataUrlAsync(
            ["master-signatures", "vehicles", vehicleId.ToString("N"), "vehicle-owner", userId],
            "driver",
            dataUrl,
            ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        var updated = await db.Vehicles
            .Where(x => x.Id == vehicleId && x.IsActive &&
                x.AssignedDriverId == userId &&
                x.AccountDriverSignedAt == null &&
                x.AccountDriverSignatureFileUrl == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.AccountDriverSignatureFileUrl, stored.RelativeUrl)
                .SetProperty(x => x.AccountDriverSignatureHash, stored.Sha256Hash)
                .SetProperty(x => x.AccountDriverSignedAt, stored.SavedAt)
                .SetProperty(x => x.AccountDriverSignedByUserId, userId)
                .SetProperty(x => x.UpdatedAt, stored.SavedAt)
                .SetProperty(x => x.UpdatedBy, userId),
                ct);

        if (updated != 1)
            throw new InvalidOperationException("Không thể lưu chữ ký. Xe có thể đã được chuyển sang tài khoản khác hoặc đã có chữ ký.");

        return stored.RelativeUrl;
    }

}
