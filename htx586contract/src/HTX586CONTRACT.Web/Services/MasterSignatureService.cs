using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Web.Services;

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

        await using var db = await factory.CreateDbContextAsync(ct);
        var admin = await (from user in db.Users
                           join userRole in db.UserRoles on user.Id equals userRole.UserId
                           join role in db.Roles on userRole.RoleId equals role.Id
                           where user.Id == adminId &&
                                 user.IsActive && !user.IsDeleted &&
                                 role.Name == "Admin"
                           select user).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản Admin đang hoạt động.");

        var oldUrl = admin.CompanySignatureFileUrl;
        StoredUploadFile? stored = null;
        try
        {
            stored = await storage.SaveImageDataUrlAsync(
                ["master-signatures", "admins", adminId],
                "company",
                dataUrl,
                ct);

            admin.CompanySignatureFileUrl = stored.RelativeUrl;
            admin.CompanySignatureHash = stored.Sha256Hash;
            admin.CompanySignedAt = stored.SavedAt;
            admin.UpdatedAt = stored.SavedAt;
            await db.SaveChangesAsync(ct);

            if (!string.Equals(oldUrl, stored.RelativeUrl, StringComparison.OrdinalIgnoreCase))
                storage.DeleteIfExists(oldUrl);

            return stored.RelativeUrl;
        }
        catch
        {
            if (stored is not null)
                storage.DeleteIfExists(stored.RelativeUrl);
            throw;
        }
    }
}
