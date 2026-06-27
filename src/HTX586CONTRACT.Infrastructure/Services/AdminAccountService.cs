using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Application.Admins.AdminAccounts;
using HTX586CONTRACT.Application.Common;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Infrastructure.Services;

public sealed class AdminAccountService(
    IDbContextFactory<ApplicationDbContext> factory,
    UserManager<ApplicationUser> userManager) : IAdminAccountService
{
    public async Task<IReadOnlyList<AdminAccountListItem>> GetAccountsAsync(string? keyword = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.Trim();
            query = query.Where(x =>
                (x.UserName != null && x.UserName.Contains(value)) ||
                x.FullName.Contains(value) ||
                (x.EmployeeCode != null && x.EmployeeCode.Contains(value)) ||
                (x.PhoneNumber != null && x.PhoneNumber.Contains(value)) ||
                (x.Email != null && x.Email.Contains(value)));
        }

        return await query.OrderBy(x => x.FullName)
            .Select(x => new AdminAccountListItem
            {
                Id = x.Id,
                UserName = x.UserName ?? string.Empty,
                FullName = x.FullName,
                EmployeeCode = x.EmployeeCode,
                PhoneNumber = x.PhoneNumber,
                Email = x.Email,
                IsActive = x.IsActive,
                MustChangePassword = x.MustChangePassword
            })
            .ToListAsync(ct);
    }

    public async Task<AdminAccountDetail?> GetDetailAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new AdminAccountDetail
            {
                Id = x.Id,
                UserName = x.UserName ?? string.Empty,
                FullName = x.FullName,
                EmployeeCode = x.EmployeeCode,
                PhoneNumber = x.PhoneNumber,
                Email = x.Email,
                IsActive = x.IsActive,
                MustChangePassword = x.MustChangePassword,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ServiceResult> UpdateAccountAsync(UpdateAdminAccountRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId)) return ServiceResult.Failure("Thiếu mã tài khoản.");
        if (string.IsNullOrWhiteSpace(request.FullName)) return ServiceResult.Failure("Vui lòng nhập họ và tên.");

        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null) return ServiceResult.Failure("Không tìm thấy tài khoản.");

        user.FullName = request.FullName.Trim();
        user.EmployeeCode = N(request.EmployeeCode);
        user.PhoneNumber = N(request.PhoneNumber);
        user.Email = N(request.Email);
        user.IsActive = request.IsActive;
        user.MustChangePassword = request.MustChangePassword;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await userManager.UpdateAsync(user);
        return result.Succeeded
            ? ServiceResult.Success("Cập nhật tài khoản thành công.")
            : ServiceResult.Failure(result.Errors.Select(x => x.Description));
    }

    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
