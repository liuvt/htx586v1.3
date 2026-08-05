using System.Linq.Expressions;
using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Application.Admins.DriverAccounts;
using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Infrastructure.Services;

/// <summary>
/// Quản lý tài khoản VehicleOwner. Tên lớp/interface cũ được giữ để tránh phá vỡ
/// các trang và migration đang sử dụng, nhưng toàn bộ nghiệp vụ đã chuyển sang role VehicleOwner.
/// </summary>
public sealed class DriverAccountService(
    UserManager<ApplicationUser> userManager,
    IDbContextFactory<ApplicationDbContext> factory) : IDriverAccountService
{
    private const string VehicleOwnerRole = "VehicleOwner";
    private const string SharedResetPassword = "Htx@586";

    public async Task<string> CreateAsync(CreateDriverAccountRequest request, CancellationToken ct = default)
    {
        ValidateBaseAccount(request.UserName, request.Password, request.FullName);
        var phone = VietnamPhoneNumber.NormalizeOrThrow(request.PhoneNumber);
        await EnsureLoginIdentifiersAvailableAsync(request.UserName, phone, null, ct);

        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim(),
            FullName = request.FullName.Trim(),
            EmployeeCode = N(request.EmployeeCode),
            PhoneNumber = phone,
            Email = N(request.Email),
            CitizenId = N(request.CitizenId),
            CitizenIdIssuedDate = request.CitizenIdIssuedDate,
            CitizenIdIssuedPlace = N(request.CitizenIdIssuedPlace),
            DateOfBirth = request.DateOfBirth,
            Address = N(request.Address),
            AreaCode = N(request.AreaCode),
            DriverLicenseNumber = N(request.DriverLicenseNumber),
            DriverLicenseClass = N(request.DriverLicenseClass),
            DriverLicenseIssuedDate = request.DriverLicenseIssuedDate,
            DriverLicenseExpiryDate = request.DriverLicenseExpiryDate,
            RegistrationStatus = "Approved",
            IsActive = true,
            MustChangePassword = request.MustChangePassword,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = N(request.CreatedByUserId)
        };

        Ensure(await userManager.CreateAsync(user, request.Password));
        try
        {
            Ensure(await userManager.AddToRoleAsync(user, VehicleOwnerRole));
            return user.Id;
        }
        catch
        {
            await userManager.DeleteAsync(user);
            throw;
        }
    }

    public async Task<string> SubmitRegistrationAsync(SelfRegisterDriverRequest request, CancellationToken ct = default)
    {
        ValidateBaseAccount(request.UserName, request.Password, request.FullName);
        var phone = VietnamPhoneNumber.NormalizeOrThrow(request.PhoneNumber);
        await EnsureLoginIdentifiersAvailableAsync(request.UserName, phone, null, ct);

        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim(),
            FullName = request.FullName.Trim(),
            PhoneNumber = phone,
            RegistrationStatus = "Pending",
            RegistrationRequestedAt = DateTime.UtcNow,
            IsActive = false,
            MustChangePassword = false,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = "SELF_REGISTRATION"
        };

        Ensure(await userManager.CreateAsync(user, request.Password));
        try
        {
            Ensure(await userManager.AddToRoleAsync(user, VehicleOwnerRole));
            return user.Id;
        }
        catch
        {
            await userManager.DeleteAsync(user);
            throw;
        }
    }

    public async Task<IReadOnlyList<DriverRegistrationRequestDto>> GetPendingRegistrationsAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await PendingRegistrationQuery(db)
            .OrderByDescending(x => x.RegistrationRequestedAt)
            .Select(RegistrationProjection)
            .ToListAsync(ct);
    }

    public async Task<int> GetUnseenPendingRegistrationCountAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await PendingRegistrationQuery(db)
            .CountAsync(x => x.RegistrationViewedAt == null, ct);
    }

    public async Task<DriverRegistrationRequestDto?> GetRegistrationDetailAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await PendingRegistrationQuery(db)
            .Where(x => x.Id == userId)
            .Select(RegistrationProjection)
            .FirstOrDefaultAsync(ct);
    }

    public async Task MarkRegistrationViewedAsync(string userId, string viewerUserId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Users
            .Where(x => x.Id == userId && !x.IsDeleted && x.RegistrationStatus == "Pending" && x.RegistrationViewedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RegistrationViewedAt, DateTime.UtcNow)
                .SetProperty(x => x.RegistrationViewedByUserId, viewerUserId)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow)
                .SetProperty(x => x.UpdatedByUserId, viewerUserId), ct);
    }

    public async Task ReviewRegistrationAsync(
        string userId,
        bool approve,
        string? note,
        string reviewerUserId,
        CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu đăng ký.");
        EnsureNotDeleted(user, "Không tìm thấy yêu cầu đăng ký.");
        await EnsureVehicleOwnerRoleAsync(user);

        if (!string.Equals(user.RegistrationStatus, "Pending", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Yêu cầu đăng ký này đã được xử lý.");

        var now = DateTime.UtcNow;
        user.RegistrationStatus = approve ? "Approved" : "Rejected";
        user.IsActive = approve;
        user.MustChangePassword = false;
        user.RegistrationViewedAt ??= now;
        user.RegistrationViewedByUserId ??= reviewerUserId;
        user.RegistrationReviewedAt = now;
        user.RegistrationReviewedByUserId = reviewerUserId;
        user.RegistrationReviewNote = N(note);
        user.UpdatedAt = now;
        user.UpdatedByUserId = reviewerUserId;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        Ensure(await userManager.UpdateAsync(user));
    }

    public async Task UpdateAsync(string userId, UpdateDriverAccountRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new InvalidOperationException("Vui lòng nhập họ tên tài khoản Chủ xe.");
        if (string.IsNullOrWhiteSpace(request.CitizenId) ||
            !request.CitizenIdIssuedDate.HasValue ||
            string.IsNullOrWhiteSpace(request.CitizenIdIssuedPlace) ||
            string.IsNullOrWhiteSpace(request.Address))
            throw new InvalidOperationException("Vui lòng nhập đầy đủ CCCD, ngày cấp, nơi cấp và địa chỉ Chủ xe.");

        var phone = VietnamPhoneNumber.NormalizeOrThrow(request.PhoneNumber);
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản Chủ xe.");
        EnsureNotDeleted(user, "Không tìm thấy tài khoản Chủ xe.");
        await EnsureVehicleOwnerRoleAsync(user);
        await EnsureLoginIdentifiersAvailableAsync(user.UserName ?? string.Empty, phone, user.Id, ct);

        var activeChanged = user.IsActive != request.IsActive;
        user.FullName = request.FullName.Trim();
        user.EmployeeCode = N(request.EmployeeCode);
        user.PhoneNumber = phone;
        user.Email = N(request.Email);
        user.CitizenId = N(request.CitizenId);
        user.CitizenIdIssuedDate = request.CitizenIdIssuedDate;
        user.CitizenIdIssuedPlace = N(request.CitizenIdIssuedPlace);
        user.DateOfBirth = request.DateOfBirth;
        user.Address = N(request.Address);
        user.AreaCode = N(request.AreaCode);
        user.DriverLicenseNumber = N(request.DriverLicenseNumber);
        user.DriverLicenseClass = N(request.DriverLicenseClass);
        user.DriverLicenseIssuedDate = request.DriverLicenseIssuedDate;
        user.DriverLicenseExpiryDate = request.DriverLicenseExpiryDate;
        user.MustChangePassword = request.MustChangePassword;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedByUserId = N(request.UpdatedByUserId);
        Ensure(await userManager.UpdateAsync(user));
        await SyncOwnedVehicleSnapshotsAsync(user, request.UpdatedByUserId, ct);

        if (activeChanged)
            await SetActiveAsync(userId, request.IsActive, ct);
    }

    public async Task<DriverAccountDetailDto?> GetDetailAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var user = await VehicleOwnerUsers(db)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return null;

        var vehicleEntities = await db.Vehicles.AsNoTracking()
            .Include(x => x.OfficeVehicles)
                .ThenInclude(x => x.CompanyProfile)
            .Where(x => x.AssignedDriverId == userId && !x.IsDeleted)
            .OrderBy(x => x.PlateNumber)
            .ToListAsync(ct);
        var vehicles = vehicleEntities.Select(x => new
        {
            x.PlateNumber,
            x.AccountDriverSignatureFileUrl,
            x.AccountDriverSignedAt,
            CompanyNames = x.OfficeVehicles
                .Where(ov => ov.IsActive && !ov.IsDeleted && ov.AssignedTo == null &&
                             ov.CompanyProfile.IsActive && !ov.CompanyProfile.IsDeleted)
                .Select(ov => string.IsNullOrWhiteSpace(ov.CompanyProfile.BranchName)
                    ? ov.CompanyProfile.CompanyName
                    : $"{ov.CompanyProfile.CompanyName} - {ov.CompanyProfile.BranchName}")
                .Distinct()
                .ToArray()
        }).ToList();

        var signedVehicle = vehicles
            .Where(x => x.AccountDriverSignedAt.HasValue && !string.IsNullOrWhiteSpace(x.AccountDriverSignatureFileUrl))
            .OrderByDescending(x => x.AccountDriverSignedAt)
            .FirstOrDefault();

        return new DriverAccountDetailDto
        {
            UserId = user.Id,
            UserName = user.UserName ?? string.Empty,
            FullName = user.FullName,
            EmployeeCode = user.EmployeeCode,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email,
            CitizenId = user.CitizenId,
            CitizenIdIssuedDate = user.CitizenIdIssuedDate,
            CitizenIdIssuedPlace = user.CitizenIdIssuedPlace,
            DateOfBirth = user.DateOfBirth,
            Address = user.Address,
            AreaCode = user.AreaCode,
            DriverLicenseNumber = user.DriverLicenseNumber,
            DriverLicenseClass = user.DriverLicenseClass,
            DriverLicenseIssuedDate = user.DriverLicenseIssuedDate,
            DriverLicenseExpiryDate = user.DriverLicenseExpiryDate,
            DriverSignatureFileUrl = signedVehicle?.AccountDriverSignatureFileUrl,
            DriverSignedAt = signedVehicle?.AccountDriverSignedAt,
            DriverSignatureIsActive = signedVehicle is not null,
            VehicleCount = vehicles.Count,
            SignedVehicleCount = vehicles.Count(x => x.AccountDriverSignedAt != null),
            VehiclePlates = string.Join(", ", vehicles.Select(x => x.PlateNumber).OrderBy(x => x)),
            CompanyNames = string.Join(", ", vehicles.SelectMany(x => x.CompanyNames).Distinct().OrderBy(x => x)),
            IsActive = user.IsActive,
            MustChangePassword = user.MustChangePassword,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public async Task<IReadOnlyList<DriverAccountDto>> GetListAsync(DriverAccountFilter filter, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = VehicleOwnerUsers(db).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();
            query = query.Where(x =>
                x.FullName.Contains(keyword) ||
                (x.UserName ?? string.Empty).Contains(keyword) ||
                (x.EmployeeCode ?? string.Empty).Contains(keyword) ||
                (x.PhoneNumber ?? string.Empty).Contains(keyword) ||
                db.Vehicles.Any(v => v.AssignedDriverId == x.Id && !v.IsDeleted && v.PlateNumber.Contains(keyword)));
        }

        if (filter.IsActive.HasValue)
            query = query.Where(x => x.IsActive == filter.IsActive.Value);

        if (filter.CompanyProfileId.HasValue)
        {
            var companyId = filter.CompanyProfileId.Value;
            query = query.Where(x => db.Vehicles.Any(v =>
                v.AssignedDriverId == x.Id && !v.IsDeleted &&
                v.OfficeVehicles.Any(ov => ov.IsActive && !ov.IsDeleted && ov.AssignedTo == null &&
                                           ov.CompanyProfileId == companyId)));
        }

        var page = Math.Max(1, filter.Page);
        var pageSize = filter.PageSize <= 0 ? 500 : Math.Clamp(filter.PageSize, 1, 500);
        var users = await query
            .OrderBy(x => x.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var ids = users.Select(x => x.Id).ToArray();
        var vehicleEntities = await db.Vehicles.AsNoTracking()
            .Include(x => x.OfficeVehicles)
                .ThenInclude(x => x.CompanyProfile)
            .Where(x => x.AssignedDriverId != null && ids.Contains(x.AssignedDriverId) && !x.IsDeleted)
            .ToListAsync(ct);
        var vehicles = vehicleEntities.Select(x => new
        {
            UserId = x.AssignedDriverId!,
            x.PlateNumber,
            x.AccountDriverSignatureFileUrl,
            x.AccountDriverSignedAt,
            CompanyNames = x.OfficeVehicles
                .Where(ov => ov.IsActive && !ov.IsDeleted && ov.AssignedTo == null &&
                             ov.CompanyProfile.IsActive && !ov.CompanyProfile.IsDeleted)
                .Select(ov => string.IsNullOrWhiteSpace(ov.CompanyProfile.BranchName)
                    ? ov.CompanyProfile.CompanyName
                    : $"{ov.CompanyProfile.CompanyName} - {ov.CompanyProfile.BranchName}")
                .Distinct()
                .ToArray()
        }).ToList();

        var byUser = vehicles.GroupBy(x => x.UserId).ToDictionary(x => x.Key, x => x.ToList());
        return users.Select(user =>
        {
            var assigned = byUser.GetValueOrDefault(user.Id) ?? [];
            return new DriverAccountDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                FullName = user.FullName,
                EmployeeCode = user.EmployeeCode,
                PhoneNumber = user.PhoneNumber,
                Email = user.Email,
                CitizenId = user.CitizenId,
                DriverLicenseNumber = user.DriverLicenseNumber,
                DriverLicenseClass = user.DriverLicenseClass,
                DriverSignatureIsActive = assigned.Any(x => x.AccountDriverSignedAt != null),
                VehicleCount = assigned.Count,
                SignedVehicleCount = assigned.Count(x => x.AccountDriverSignedAt != null),
                VehiclePlates = string.Join(", ", assigned.Select(x => x.PlateNumber).OrderBy(x => x)),
                CompanyNames = string.Join(", ", assigned.SelectMany(x => x.CompanyNames).Distinct().OrderBy(x => x)),
                IsActive = user.IsActive,
                MustChangePassword = user.MustChangePassword,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }).ToList();
    }

    public Task SetActiveAsync(string userId, bool isActive, CancellationToken ct = default) =>
        ChangeOperationalStateAsync(userId, isActive, markDeleted: false, isActive ? "VEHICLE_OWNER_UNLOCKED" : "VEHICLE_OWNER_LOCKED", ct);

    public async Task ResetPasswordAsync(string userId, string newPassword, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản Chủ xe.");
        EnsureNotDeleted(user, "Không tìm thấy tài khoản Chủ xe.");
        await EnsureVehicleOwnerRoleAsync(user);

        // Mật khẩu reset dùng chung theo yêu cầu nghiệp vụ, không nhận giá trị tùy ý từ giao diện.
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        Ensure(await userManager.ResetPasswordAsync(user, token, SharedResetPassword));
        user.MustChangePassword = true;
        user.UpdatedAt = DateTime.UtcNow;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        Ensure(await userManager.UpdateAsync(user));
    }

    public async Task RequirePasswordChangeAsync(string userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản Chủ xe.");
        EnsureNotDeleted(user, "Không tìm thấy tài khoản Chủ xe.");
        await EnsureVehicleOwnerRoleAsync(user);
        user.MustChangePassword = true;
        user.UpdatedAt = DateTime.UtcNow;
        Ensure(await userManager.UpdateAsync(user));
    }

    public Task DeleteAsync(string userId, CancellationToken ct = default) =>
        ChangeOperationalStateAsync(userId, active: false, markDeleted: true, "VEHICLE_OWNER_SOFT_DELETED", ct);

    private async Task ChangeOperationalStateAsync(
        string userId,
        bool active,
        bool markDeleted,
        string source,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản Chủ xe.");
        var isVehicleOwner = await (from userRole in db.UserRoles
                                    join role in db.Roles on userRole.RoleId equals role.Id
                                    where userRole.UserId == userId && role.Name == VehicleOwnerRole
                                    select userRole.UserId).AnyAsync(ct);
        if (!isVehicleOwner)
            throw new KeyNotFoundException("Không tìm thấy tài khoản Chủ xe.");

        if (active && !string.Equals(user.RegistrationStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ tài khoản đã được duyệt mới được mở khóa.");

        var now = DateTime.UtcNow;
        user.IsActive = active && !markDeleted;
        user.UpdatedAt = now;
        user.UpdatedByUserId = source;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ConcurrencyStamp = Guid.NewGuid().ToString("N");

        if (markDeleted)
        {
            user.IsDeleted = true;
            user.DeletedAt = now;
            user.DeletedBy = source;
        }

        // Khi khóa/xóa, trả toàn bộ xe về trạng thái chưa cấp. Dữ liệu hợp đồng đã snapshot không đổi.
        if (!user.IsActive)
        {
            await db.Vehicles
                .Where(x => x.AssignedDriverId == userId && !x.IsDeleted)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.AssignedDriverId, (string?)null)
                    .SetProperty(x => x.UpdatedAt, now)
                    .SetProperty(x => x.UpdatedBy, source), ct);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }


    private async Task SyncOwnedVehicleSnapshotsAsync(
        ApplicationUser owner,
        string? actorUserId,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var vehicles = await db.Vehicles
            .Where(x => x.AssignedDriverId == owner.Id && !x.IsDeleted)
            .ToListAsync(ct);
        if (vehicles.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var vehicle in vehicles)
        {
            vehicle.OwnerName = owner.FullName.Trim();
            vehicle.OwnerPhoneNumber = N(owner.PhoneNumber);
            vehicle.OwnerCitizenId = N(owner.CitizenId);
            vehicle.OwnerCitizenIdIssuedDate = owner.CitizenIdIssuedDate?.Date;
            vehicle.OwnerCitizenIdIssuedPlace = N(owner.CitizenIdIssuedPlace);
            vehicle.OwnerAddress = N(owner.Address);
            vehicle.UpdatedAt = now;
            vehicle.UpdatedBy = N(actorUserId) ?? owner.Id;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureLoginIdentifiersAvailableAsync(
        string userName,
        string phoneNumber,
        string? excludedUserId,
        CancellationToken ct)
    {
        var normalizedName = userManager.NormalizeName(userName.Trim());
        var normalizedPhoneAsName = userManager.NormalizeName(phoneNumber);
        await using var db = await factory.CreateDbContextAsync(ct);
        var candidates = await db.Users.AsNoTracking()
            .Where(x => x.Id != excludedUserId && !x.IsDeleted &&
                (x.NormalizedUserName == normalizedName ||
                 x.NormalizedUserName == normalizedPhoneAsName ||
                 x.PhoneNumber != null))
            .Select(x => new { x.NormalizedUserName, x.PhoneNumber })
            .ToListAsync(ct);

        var conflict = candidates.Any(x =>
            x.NormalizedUserName == normalizedName ||
            x.NormalizedUserName == normalizedPhoneAsName ||
            (VietnamPhoneNumber.TryNormalize(x.PhoneNumber, out var storedPhone) && storedPhone == phoneNumber));
        if (conflict)
            throw new InvalidOperationException("ID đăng nhập hoặc số điện thoại đang được tài khoản khác sử dụng.");
    }

    private async Task EnsureVehicleOwnerRoleAsync(ApplicationUser user)
    {
        if (!await userManager.IsInRoleAsync(user, VehicleOwnerRole))
            throw new KeyNotFoundException("Không tìm thấy tài khoản Chủ xe.");
    }

    private static IQueryable<ApplicationUser> VehicleOwnerUsers(ApplicationDbContext db) =>
        from user in db.Users
        join userRole in db.UserRoles on user.Id equals userRole.UserId
        join role in db.Roles on userRole.RoleId equals role.Id
        where role.Name == VehicleOwnerRole && !user.IsDeleted
        select user;

    private static IQueryable<ApplicationUser> PendingRegistrationQuery(ApplicationDbContext db) =>
        VehicleOwnerUsers(db).AsNoTracking()
            .Where(x => x.RegistrationStatus == "Pending");

    private static readonly Expression<Func<ApplicationUser, DriverRegistrationRequestDto>> RegistrationProjection = x =>
        new DriverRegistrationRequestDto
        {
            UserId = x.Id,
            UserName = x.UserName ?? string.Empty,
            FullName = x.FullName,
            PhoneNumber = x.PhoneNumber,
            RequestedAt = x.RegistrationRequestedAt ?? x.CreatedAt,
            ViewedAt = x.RegistrationViewedAt
        };

    private static void ValidateBaseAccount(string userName, string password, string fullName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            throw new InvalidOperationException("Vui lòng nhập ID đăng nhập.");
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Vui lòng nhập mật khẩu.");
        if (string.IsNullOrWhiteSpace(fullName))
            throw new InvalidOperationException("Vui lòng nhập họ tên.");
    }

    private static void EnsureNotDeleted(ApplicationUser user, string message)
    {
        if (user.IsDeleted) throw new KeyNotFoundException(message);
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }

    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
