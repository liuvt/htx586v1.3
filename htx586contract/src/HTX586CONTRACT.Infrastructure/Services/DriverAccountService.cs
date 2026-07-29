using System.Linq.Expressions;
using System.Security.Cryptography;
using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Application.Admins.DriverAccounts;
using HTX586CONTRACT.Application.Common;
using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HTX586CONTRACT.Infrastructure.Services;

/// <summary>
/// Tài xế thuộc trực tiếp một Admin. CompanyProfileId chỉ được ghi kèm để tương
/// thích dữ liệu cũ và không còn là khóa nghiệp vụ của luồng mới.
/// </summary>
public sealed class DriverAccountService(
    UserManager<ApplicationUser> userManager,
    IDbContextFactory<ApplicationDbContext> factory,
    IHostEnvironment environment,
    IOptions<DataStorageOptions> dataStorageOptions,
    IOptions<FileStorageOptions> fileStorageOptions) : IDriverAccountService
{
    public async Task<string> SubmitRegistrationAsync(SelfRegisterDriverRequest request, CancellationToken ct = default)
    {
        ValidateAdmin(request.AdminId);
        ValidateLogin(request.UserName, request.Password, request.FullName);
        var phoneNumber = VietnamPhoneNumber.NormalizeOrThrow(request.PhoneNumber);
        ValidateRegistrationProfile(request);
        var admin = await EnsureAdminAsync(request.AdminId, ct);
        await EnsureLoginIdentifiersAvailableAsync(request.UserName, phoneNumber, null, ct);

        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim(),
            FullName = request.FullName.Trim(),
            PhoneNumber = phoneNumber,
            AdminId = admin.Id,
            CompanyProfileId = admin.CompanyProfileId,
            DateOfBirth = request.DateOfBirth,
            AreaCode = N(request.AreaCode),
            Address = N(request.Address),
            CitizenId = N(request.CitizenId),
            CitizenIdIssuedDate = request.CitizenIdIssuedDate,
            CitizenIdIssuedPlace = N(request.CitizenIdIssuedPlace),
            DriverLicenseNumber = N(request.DriverLicenseNumber),
            DriverLicenseClass = N(request.DriverLicenseClass),
            DriverLicenseIssuedDate = request.DriverLicenseIssuedDate,
            DriverLicenseExpiryDate = request.DriverLicenseExpiryDate,
            RegistrationStatus = "Pending",
            RegistrationRequestedAt = DateTime.UtcNow,
            IsActive = false,
            MustChangePassword = false,
            CreatedAt = DateTime.UtcNow
        };

        Ensure(await userManager.CreateAsync(user, request.Password));
        try
        {
            Ensure(await userManager.AddToRoleAsync(user, "Driver"));
            var stored = await SaveRegistrationSignatureAsync(user.Id, request.SignatureDataUrl, ct);
            user.DriverSignatureFileUrl = stored.Url;
            user.DriverSignatureHash = stored.Hash;
            user.DriverSignedAt = stored.SavedAt;
            user.DriverSignatureIsActive = true;
            user.DriverSignatureInactiveAt = null;
            Ensure(await userManager.UpdateAsync(user));
            return user.Id;
        }
        catch
        {
            user.IsDeleted = true;
            user.DeletedAt = DateTime.UtcNow;
            user.DeletedBy = "SELF_REGISTRATION_ROLLBACK";
            user.IsActive = false;
            Ensure(await userManager.UpdateAsync(user));
            throw;
        }
    }

    public async Task<IReadOnlyList<DriverRegistrationRequestDto>> GetPendingRegistrationsAsync(string adminId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await PendingRegistrationQuery(db, adminId)
            .OrderByDescending(x => x.RegistrationRequestedAt)
            .Select(RegistrationProjection)
            .ToListAsync(ct);
    }

    public async Task<int> GetUnseenPendingRegistrationCountAsync(string adminId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await PendingRegistrationQuery(db, adminId).CountAsync(x => x.RegistrationViewedAt == null, ct);
    }

    public async Task<DriverRegistrationRequestDto?> GetRegistrationDetailAsync(string userId, string adminId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await PendingRegistrationQuery(db, adminId)
            .Where(x => x.Id == userId)
            .Select(RegistrationProjection)
            .FirstOrDefaultAsync(ct);
    }

    public async Task MarkRegistrationViewedAsync(string userId, string viewerUserId, CancellationToken ct = default)
    {
        await EnsureAdminAsync(viewerUserId, ct);
        await using var db = await factory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;
        var changed = await db.Users
            .Where(x => x.Id == userId && x.AdminId == viewerUserId && !x.IsDeleted && x.RegistrationStatus == "Pending" && x.RegistrationViewedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RegistrationViewedAt, now)
                .SetProperty(x => x.RegistrationViewedByUserId, viewerUserId)
                .SetProperty(x => x.UpdatedAt, now), ct);
        if (changed == 0 && !await db.Users.AnyAsync(x => x.Id == userId && x.AdminId == viewerUserId && x.RegistrationStatus == "Pending", ct))
            throw new KeyNotFoundException("Không tìm thấy yêu cầu đăng ký thuộc công ty của bạn.");
    }

    public async Task ReviewRegistrationAsync(string userId, bool approve, string? note, string reviewerUserId, CancellationToken ct = default)
    {
        await EnsureAdminAsync(reviewerUserId, ct);
        var user = await userManager.FindByIdAsync(userId) ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu đăng ký.");
        EnsureNotDeleted(user, "Không tìm thấy yêu cầu đăng ký.");
        await EnsureDriverRoleAsync(user);
        if (!string.Equals(user.AdminId, reviewerUserId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Yêu cầu đăng ký không thuộc công ty của bạn.");
        if (!string.Equals(user.RegistrationStatus, "Pending", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Yêu cầu này đã được xử lý.");
        if (approve && string.IsNullOrWhiteSpace(user.DriverSignatureFileUrl))
            throw new InvalidOperationException("Không thể duyệt tài xế chưa có chữ ký cố định.");

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
        Ensure(await userManager.UpdateAsync(user));
    }

    public async Task UpdateAsync(string id, UpdateDriverAccountRequest request, CancellationToken ct = default)
    {
        ValidateAdmin(request.AdminId);
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new InvalidOperationException("Vui lòng nhập họ tên tài xế.");
        var phoneNumber = VietnamPhoneNumber.NormalizeOrThrow(request.PhoneNumber);
        var admin = await EnsureAdminAsync(request.AdminId, ct);
        var user = await userManager.FindByIdAsync(id) ?? throw new KeyNotFoundException("Không tìm thấy tài xế.");
        EnsureNotDeleted(user, "Không tìm thấy tài xế.");
        await EnsureDriverRoleAsync(user);
        await EnsureLoginIdentifiersAvailableAsync(user.UserName ?? string.Empty, phoneNumber, user.Id, ct);

        var wasActive = user.IsActive;
        user.AdminId = admin.Id;
        user.CompanyProfileId = admin.CompanyProfileId;
        user.FullName = request.FullName.Trim();
        user.EmployeeCode = N(request.EmployeeCode);
        user.PhoneNumber = phoneNumber;
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
        user.MustChangePassword = user.RegistrationRequestedAt is null && user.DriverSignedAt is null
            ? true
            : request.MustChangePassword;
        user.UpdatedAt = DateTime.UtcNow;
        Ensure(await userManager.UpdateAsync(user));

        if (!request.IsActive || wasActive != request.IsActive)
            await SetActiveAsync(id, request.IsActive, request.AdminId, ct);
    }

    public async Task<DriverAccountDetailDto?> GetDetailAsync(string id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await DriverRoleQuery(db)
            .Where(x => x.Id == id)
            .Select(x => new DriverAccountDetailDto
            {
                UserId = x.Id,
                UserName = x.UserName ?? string.Empty,
                FullName = x.FullName,
                EmployeeCode = x.EmployeeCode,
                PhoneNumber = x.PhoneNumber,
                Email = x.Email,
                AdminId = x.AdminId,
                CompanyProfileId = x.CompanyProfileId,
                CompanyName = x.AdminAccount == null ? null :
                    (string.IsNullOrWhiteSpace(x.AdminAccount.CompanyBranchName)
                        ? x.AdminAccount.CompanyName
                        : x.AdminAccount.CompanyName + " - " + x.AdminAccount.CompanyBranchName),
                CitizenId = x.CitizenId,
                CitizenIdIssuedDate = x.CitizenIdIssuedDate,
                CitizenIdIssuedPlace = x.CitizenIdIssuedPlace,
                DateOfBirth = x.DateOfBirth,
                Address = x.Address,
                AreaCode = x.AreaCode,
                DriverLicenseNumber = x.DriverLicenseNumber,
                DriverLicenseClass = x.DriverLicenseClass,
                DriverLicenseIssuedDate = x.DriverLicenseIssuedDate,
                DriverLicenseExpiryDate = x.DriverLicenseExpiryDate,
                DriverSignatureFileUrl = x.DriverSignatureFileUrl,
                DriverSignedAt = x.DriverSignedAt,
                DriverSignatureIsActive = x.DriverSignatureIsActive,
                DriverSignatureInactiveAt = x.DriverSignatureInactiveAt,
                IsActive = x.IsActive,
                MustChangePassword = x.MustChangePassword,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            }).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<DriverAccountDto>> GetListAsync(DriverAccountFilter filter, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = DriverRoleQuery(db);
        if (!string.IsNullOrWhiteSpace(filter.AdminId)) query = query.Where(x => x.AdminId == filter.AdminId);
        if (filter.CompanyProfileId.HasValue) query = query.Where(x => x.CompanyProfileId == filter.CompanyProfileId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();
            query = query.Where(x =>
                x.FullName.Contains(keyword) ||
                (x.UserName ?? string.Empty).Contains(keyword) ||
                (x.EmployeeCode ?? string.Empty).Contains(keyword) ||
                (x.PhoneNumber ?? string.Empty).Contains(keyword) ||
                (x.CitizenId ?? string.Empty).Contains(keyword));
        }
        if (filter.IsActive.HasValue) query = query.Where(x => x.IsActive == filter.IsActive.Value);

        IQueryable<ApplicationUser> paged = query.OrderBy(x => x.FullName);
        if (filter.PageSize > 0)
        {
            var page = Math.Max(1, filter.Page);
            var pageSize = Math.Clamp(filter.PageSize, 1, 500);
            paged = paged.Skip((page - 1) * pageSize).Take(pageSize);
        }

        return await paged.Select(x => new DriverAccountDto
        {
            Id = x.Id,
            UserName = x.UserName ?? string.Empty,
            FullName = x.FullName,
            EmployeeCode = x.EmployeeCode,
            PhoneNumber = x.PhoneNumber,
            Email = x.Email,
            AdminId = x.AdminId,
            CompanyName = x.AdminAccount == null ? null :
                (string.IsNullOrWhiteSpace(x.AdminAccount.CompanyBranchName)
                    ? x.AdminAccount.CompanyName
                    : x.AdminAccount.CompanyName + " - " + x.AdminAccount.CompanyBranchName),
            CitizenId = x.CitizenId,
            DriverLicenseNumber = x.DriverLicenseNumber,
            DriverLicenseClass = x.DriverLicenseClass,
            DriverSignatureFileUrl = x.DriverSignatureFileUrl,
            DriverSignatureIsActive = x.DriverSignatureIsActive,
            DriverSignatureInactiveAt = x.DriverSignatureInactiveAt,
            IsActive = x.IsActive,
            MustChangePassword = x.MustChangePassword,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        }).ToListAsync(ct);
    }

    private Task SetActiveAsync(string id, bool active, string adminId, CancellationToken ct = default)
        => ChangeOperationalStateAsync(
            id, active, markDeleted: false,
            source: active ? "ACCOUNT_UNLOCKED" : "ACCOUNT_TEMPORARILY_LOCKED",
            expectedAdminId: adminId,
            ct: ct);

    public async Task ResetPasswordAsync(string id, string password, string adminId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("Vui lòng nhập mật khẩu mới.");
        await EnsureAdminAsync(adminId, ct);
        var user = await userManager.FindByIdAsync(id) ?? throw new KeyNotFoundException("Không tìm thấy tài xế.");
        EnsureNotDeleted(user, "Không tìm thấy tài xế.");
        await EnsureDriverRoleAsync(user);
        if (!string.Equals(user.AdminId, adminId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Tài xế không thuộc công ty của bạn.");
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        Ensure(await userManager.ResetPasswordAsync(user, token, password));
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;
        Ensure(await userManager.UpdateAsync(user));
    }

    private async Task ChangeOperationalStateAsync(
        string id,
        bool active,
        bool markDeleted,
        string source,
        string expectedAdminId,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy tài xế.");
        if (user.IsDeleted || !await IsInRoleAsync(db, id, "Driver", ct))
            throw new KeyNotFoundException("Không tìm thấy tài xế.");
        if (!string.Equals(user.AdminId, expectedAdminId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Tài xế không thuộc công ty của bạn.");
        if (active)
        {
            if (!string.Equals(user.RegistrationStatus, "Approved", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Chỉ tài xế đã được duyệt mới được mở khóa.");
            if (string.IsNullOrWhiteSpace(user.AdminId) || !await IsInRoleAsync(db, user.AdminId, "Admin", ct))
                throw new InvalidOperationException("Tài xế chưa thuộc tài khoản Admin hợp lệ.");
        }

        var now = DateTime.UtcNow;
        user.IsActive = active && !markDeleted;
        user.UpdatedAt = now;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        if (user.IsActive)
        {
            user.LockoutEnd = null;
            user.AccessFailedCount = 0;
        }
        if (markDeleted)
        {
            user.IsDeleted = true;
            user.DeletedAt = now;
            user.DeletedBy = source;
        }
        if (!user.IsActive)
        {
            await db.Vehicles.Where(x => x.AssignedDriverId == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.AssignedDriverId, (string?)null)
                    .SetProperty(x => x.UpdatedAt, now)
                    .SetProperty(x => x.UpdatedBy, source), ct);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<ApplicationUser> EnsureAdminAsync(string adminId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var admin = await db.Users.FirstOrDefaultAsync(x => x.Id == adminId && x.IsActive && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Tài khoản Admin/công ty không tồn tại hoặc đã ngừng hoạt động.");
        if (!await IsInRoleAsync(db, adminId, "Admin", ct))
            throw new InvalidOperationException("Tài khoản được chọn không phải Admin.");
        return admin;
    }

    private static IQueryable<ApplicationUser> DriverRoleQuery(ApplicationDbContext db) =>
        from user in db.Users.AsNoTracking()
        join userRole in db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
        join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
        where role.Name == "Driver" && !user.IsDeleted
        select user;

    private static IQueryable<ApplicationUser> PendingRegistrationQuery(ApplicationDbContext db, string adminId) =>
        DriverRoleQuery(db).Where(x => x.AdminId == adminId && x.RegistrationStatus == "Pending");

    private static readonly Expression<Func<ApplicationUser, DriverRegistrationRequestDto>> RegistrationProjection = x => new()
    {
        UserId = x.Id,
        UserName = x.UserName ?? string.Empty,
        FullName = x.FullName,
        PhoneNumber = x.PhoneNumber,
        AdminId = x.AdminId,
        CompanyProfileId = x.CompanyProfileId,
        CompanyName = x.AdminAccount == null ? null :
            (string.IsNullOrWhiteSpace(x.AdminAccount.CompanyBranchName)
                ? x.AdminAccount.CompanyName
                : x.AdminAccount.CompanyName + " - " + x.AdminAccount.CompanyBranchName),
        DateOfBirth = x.DateOfBirth,
        AreaCode = x.AreaCode,
        Address = x.Address,
        CitizenId = x.CitizenId,
        CitizenIdIssuedDate = x.CitizenIdIssuedDate,
        CitizenIdIssuedPlace = x.CitizenIdIssuedPlace,
        DriverLicenseNumber = x.DriverLicenseNumber,
        DriverLicenseClass = x.DriverLicenseClass,
        DriverLicenseIssuedDate = x.DriverLicenseIssuedDate,
        DriverLicenseExpiryDate = x.DriverLicenseExpiryDate,
        DriverSignatureFileUrl = x.DriverSignatureFileUrl,
        RequestedAt = x.RegistrationRequestedAt ?? x.CreatedAt,
        ViewedAt = x.RegistrationViewedAt
    };

    private async Task EnsureLoginIdentifiersAvailableAsync(string userName, string phoneNumber, string? excludedUserId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var normalizedName = userManager.NormalizeName(userName.Trim());
        var normalizedPhoneAsName = userManager.NormalizeName(phoneNumber);
        var conflict = await db.Users.IgnoreQueryFilters().AsNoTracking().AnyAsync(x =>
            x.Id != excludedUserId &&
            (x.NormalizedUserName == normalizedName || x.NormalizedUserName == normalizedPhoneAsName || x.PhoneNumber == phoneNumber), ct);
        if (conflict) throw new InvalidOperationException("Tên đăng nhập hoặc số điện thoại đang được sử dụng.");
    }

    private async Task<(string Url, string Hash, DateTime SavedAt)> SaveRegistrationSignatureAsync(string userId, string dataUrl, CancellationToken ct)
    {
        var comma = dataUrl.IndexOf(',');
        if (comma < 0) throw new InvalidOperationException("Dữ liệu chữ ký không hợp lệ.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(dataUrl[(comma + 1)..]); }
        catch (FormatException) { throw new InvalidOperationException("Dữ liệu chữ ký không đúng định dạng Base64."); }
        if (bytes.Length == 0 || bytes.Length > 2 * 1024 * 1024)
            throw new InvalidOperationException("Dung lượng chữ ký không hợp lệ hoặc vượt quá 2 MB.");

        var extension = DetectSignatureExtension(bytes)
            ?? throw new InvalidOperationException("Chữ ký phải là ảnh PNG hoặc JPG hợp lệ.");

        var root = StoragePathResolver.ResolvePathUnderDataRoot(
            environment.ContentRootPath,
            dataStorageOptions.Value.RootPath,
            fileStorageOptions.Value.UploadRootPath,
            new FileStorageOptions().UploadRootPath);
        var folder = Path.Combine(root, "master-signatures", "drivers", userId);
        Directory.CreateDirectory(folder);
        var fileName = $"driver-{Guid.NewGuid():N}.{extension}";
        await File.WriteAllBytesAsync(Path.Combine(folder, fileName), bytes, ct);
        var requestPath = "/" + (fileStorageOptions.Value.PublicRequestPath ?? "/uploads").Trim('/');
        return ($"{requestPath}/master-signatures/drivers/{userId}/{fileName}", Convert.ToHexString(SHA256.HashData(bytes)), DateTime.UtcNow);
    }


    private static string? DetectSignatureExtension(byte[] bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            return "png";

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "jpg";

        return null;
    }

    private static async Task<bool> IsInRoleAsync(ApplicationDbContext db, string userId, string roleName, CancellationToken ct) =>
        await (from userRole in db.UserRoles.AsNoTracking()
               join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
               where userRole.UserId == userId && role.Name == roleName
               select userRole.UserId).AnyAsync(ct);

    private static void ValidateLogin(string userName, string password, string fullName)
    {
        if (string.IsNullOrWhiteSpace(userName)) throw new InvalidOperationException("Vui lòng nhập tên đăng nhập.");
        if (string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("Vui lòng nhập mật khẩu.");
        if (string.IsNullOrWhiteSpace(fullName)) throw new InvalidOperationException("Vui lòng nhập họ tên tài xế.");
    }

    private static void ValidateRegistrationProfile(SelfRegisterDriverRequest request)
    {
        if (request.DateOfBirth is null || string.IsNullOrWhiteSpace(request.AreaCode) || string.IsNullOrWhiteSpace(request.Address) ||
            string.IsNullOrWhiteSpace(request.CitizenId) || request.CitizenIdIssuedDate is null || string.IsNullOrWhiteSpace(request.CitizenIdIssuedPlace))
            throw new InvalidOperationException("Vui lòng nhập đầy đủ thông tin cá nhân và CCCD.");
        if (string.IsNullOrWhiteSpace(request.DriverLicenseNumber) || string.IsNullOrWhiteSpace(request.DriverLicenseClass) ||
            request.DriverLicenseIssuedDate is null || request.DriverLicenseExpiryDate is null)
            throw new InvalidOperationException("Vui lòng nhập đầy đủ thông tin giấy phép lái xe.");
        if (request.DriverLicenseExpiryDate.Value.Date < DateTime.Today)
            throw new InvalidOperationException("Giấy phép lái xe đã hết hạn.");
        if (string.IsNullOrWhiteSpace(request.SignatureDataUrl))
            throw new InvalidOperationException("Vui lòng ký tên trước khi gửi yêu cầu.");
    }

    private static void ValidateAdmin(string? adminId)
    {
        if (string.IsNullOrWhiteSpace(adminId))
            throw new InvalidOperationException("Vui lòng chọn đúng công ty/Admin tiếp nhận đăng ký.");
    }

    private static void EnsureNotDeleted(ApplicationUser user, string message)
    {
        if (user.IsDeleted) throw new KeyNotFoundException(message);
    }

    private async Task EnsureDriverRoleAsync(ApplicationUser user)
    {
        if (!await userManager.IsInRoleAsync(user, "Driver"))
            throw new KeyNotFoundException("Không tìm thấy tài xế.");
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }

    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
