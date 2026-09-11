using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Application.Contracts;
using HTX586CONTRACT.Application.Drivers;
using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Drivers;
using HTX586CONTRACT.Domain.Vehicles;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Infrastructure.Services;

public sealed class SavedDriverService(IDbContextFactory<ApplicationDbContext> factory) : ISavedDriverService
{
    public async Task<IReadOnlyList<SavedDriverDto>> GetMineAsync(
        string currentUserId,
        bool includeDeleted = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(currentUserId))
            return [];

        await using var db = await factory.CreateDbContextAsync(ct);
        var query = includeDeleted
            ? db.SavedDrivers.IgnoreQueryFilters().AsNoTracking().Where(x => x.CreatedByUserId == currentUserId)
            : db.SavedDrivers.AsNoTracking().Where(x => x.CreatedByUserId == currentUserId && !x.IsDeleted);

        return await query
            .OrderBy(x => x.IsDeleted)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new SavedDriverDto
            {
                DriverLicenseNumber = x.DriverLicenseNumber,
                FullName = x.FullName,
                DriverLicenseClass = x.DriverLicenseClass,
                PhoneNumber = x.PhoneNumber,
                CreatedAt = x.CreatedAt,
                CreatedByUserId = x.CreatedByUserId,
                CreatedByName = x.CreatedByUser.FullName,
                DeletedAt = x.DeletedAt
            })
            .ToListAsync(ct);
    }

    public async Task<SaveSavedDriverResult> SaveAsync(
        SaveSavedDriverRequest request,
        string currentUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(currentUserId))
            return new(false, "Không xác định được tài khoản tạo tài xế.");

        var licenseNumber = NormalizeLicenseNumber(request.DriverLicenseNumber);
        if (string.IsNullOrWhiteSpace(licenseNumber))
            return new(false, "Số GPLX là bắt buộc.");
        if (string.IsNullOrWhiteSpace(request.FullName))
            return new(false, "Họ tên tài xế là bắt buộc.");
        if (!AutomobileDrivingLicenseClasses.IsValid(request.DriverLicenseClass))
            return new(false, "Hạng GPLX ô tô không hợp lệ.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var existing = await db.SavedDrivers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.DriverLicenseNumber == licenseNumber, ct);
        var now = DateTime.UtcNow;

        if (existing is not null && !string.Equals(existing.CreatedByUserId, currentUserId, StringComparison.Ordinal))
        {
            // Số GPLX là khóa chính toàn cục. Không tiết lộ dữ liệu/tài khoản đã tạo bản ghi này.
            return new(false, "Không thể lưu tài xế với Số GPLX này. Vui lòng kiểm tra lại Số GPLX.");
        }

        if (existing is null)
        {
            existing = new SavedDriver
            {
                DriverLicenseNumber = licenseNumber,
                CreatedByUserId = currentUserId,
                CreatedAt = now
            };
            db.SavedDrivers.Add(existing);
        }
        else if (existing.IsDeleted)
        {
            existing.IsDeleted = false;
            existing.DeletedAt = null;
            existing.DeletedBy = null;
        }

        existing.FullName = request.FullName.Trim();
        existing.DriverLicenseClass = request.DriverLicenseClass.Trim().ToUpperInvariant();
        existing.PhoneNumber = N(request.PhoneNumber);
        existing.UpdatedAt = now;
        existing.UpdatedByUserId = currentUserId;

        await db.SaveChangesAsync(ct);
        return new(true, "Đã lưu tài xế vào danh sách gợi ý của tài khoản.");
    }

    public async Task<SaveSavedDriverResult> DeleteAsync(
        string driverLicenseNumber,
        string currentUserId,
        CancellationToken ct = default)
    {
        var licenseNumber = NormalizeLicenseNumber(driverLicenseNumber);
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.SavedDrivers.IgnoreQueryFilters().FirstOrDefaultAsync(x =>
            x.DriverLicenseNumber == licenseNumber &&
            x.CreatedByUserId == currentUserId &&
            !x.IsDeleted, ct);
        if (entity is null)
            return new(false, "Không tìm thấy tài xế trong danh sách của bạn.");

        var now = DateTime.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.DeletedBy = currentUserId;
        entity.UpdatedAt = now;
        entity.UpdatedByUserId = currentUserId;
        await db.SaveChangesAsync(ct);
        return new(true, "Đã xóa tài xế khỏi danh sách gợi ý.");
    }

    private static string NormalizeLicenseNumber(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Trim().Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static string? N(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
