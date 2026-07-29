using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Web.Services;

/// <summary>
/// Quản lý hai chữ ký cố định của luồng tinh gọn: công ty theo Admin và tài xế.
/// Hai chữ ký được chụp vào snapshot khi tạo hợp đồng mới.
/// </summary>
public sealed class MasterSignatureService(
    IDbContextFactory<ApplicationDbContext> factory,
    IUploadFileStorage storage)
{
    public async Task<string> SaveAdminCompanySignatureAsync(
        string adminId,
        string dataUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(adminId))
            throw new InvalidOperationException("Thiếu tài khoản Admin.");

        var stored = await storage.SavePngDataUrlAsync(
            ["master-signatures", "admins", adminId],
            "company",
            dataUrl,
            ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        var updated = await db.Users
            .Where(x => x.Id == adminId && !x.IsDeleted
                && db.UserRoles.Any(ur => ur.UserId == x.Id
                    && db.Roles.Any(role => role.Id == ur.RoleId && role.Name == "Admin")))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.CompanySignatureFileUrl, stored.RelativeUrl)
                .SetProperty(x => x.CompanySignatureHash, stored.Sha256Hash)
                .SetProperty(x => x.CompanySignedAt, stored.SavedAt)
                .SetProperty(x => x.UpdatedAt, stored.SavedAt), ct);

        if (updated != 1)
            throw new KeyNotFoundException("Không tìm thấy tài khoản Admin để lưu chữ ký công ty.");

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

    private async Task<string> SaveDriverSignatureAsync(
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
                    && db.Roles.Any(role => role.Id == ur.RoleId && role.Name == "Driver")))
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
}
