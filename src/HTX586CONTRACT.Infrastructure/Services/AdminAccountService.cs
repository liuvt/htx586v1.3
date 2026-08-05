using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Application.Admins.AdminAccounts;
using HTX586CONTRACT.Application.Common;
using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Domain.Offices;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Infrastructure.Services;

/// <summary>
/// Quản lý thống nhất tài khoản Admin và VehicleOwner. Admin được gán nhiều
/// Công ty/Văn phòng qua AdminOffices; VehicleOwner nhận phạm vi theo xe.
/// </summary>
public sealed class AdminAccountService(
    IDbContextFactory<ApplicationDbContext> factory,
    UserManager<ApplicationUser> userManager) : IAdminAccountService
{
    private const string AdminRole = "Admin";
    private const string VehicleOwnerRole = "VehicleOwner";
    private const string OwnerRole = "Owner";
    private const string DefaultResetPassword = "Htx@586";

    public async Task<IReadOnlyList<AdminAccountListItem>> GetAccountsAsync(
        string? keyword = null,
        string? role = null,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var managedRoleNames = new[] { AdminRole, VehicleOwnerRole };

        var managedUserIds = await (
            from ur in db.UserRoles.AsNoTracking()
            join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where r.Name != null && managedRoleNames.Contains(r.Name)
            select ur.UserId)
            .Distinct()
            .ToListAsync(ct);

        var ownerUserIds = await (
            from ur in db.UserRoles.AsNoTracking()
            join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where r.Name == OwnerRole
            select ur.UserId)
            .ToListAsync(ct);

        var query = db.Users.AsNoTracking()
            .Where(x => !x.IsDeleted && managedUserIds.Contains(x.Id) && !ownerUserIds.Contains(x.Id));

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

        var users = await query.OrderBy(x => x.FullName)
            .Select(x => new
            {
                x.Id,
                UserName = x.UserName ?? string.Empty,
                x.FullName,
                x.EmployeeCode,
                x.PhoneNumber,
                x.Email,
                x.IsActive,
                x.MustChangePassword
            })
            .ToListAsync(ct);

        var userIds = users.Select(x => x.Id).ToArray();
        var roleMap = await LoadManagedRoleMapAsync(db, userIds, ct);
        var officeMap = await LoadOfficeNameMapAsync(db, userIds, ct);
        var vehicleCountMap = await db.Vehicles.AsNoTracking()
            .Where(x => x.AssignedDriverId != null && userIds.Contains(x.AssignedDriverId))
            .GroupBy(x => x.AssignedDriverId!)
            .Select(x => new { UserId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var result = users.Select(x => new AdminAccountListItem
        {
            Id = x.Id,
            UserName = x.UserName,
            FullName = x.FullName,
            EmployeeCode = x.EmployeeCode,
            PhoneNumber = x.PhoneNumber,
            Email = x.Email,
            Role = roleMap.GetValueOrDefault(x.Id) ?? string.Empty,
            OfficeNames = officeMap.GetValueOrDefault(x.Id) ?? string.Empty,
            VehicleCount = vehicleCountMap.GetValueOrDefault(x.Id),
            IsActive = x.IsActive,
            MustChangePassword = x.MustChangePassword
        });

        if (!string.IsNullOrWhiteSpace(role))
            result = result.Where(x => string.Equals(x.Role, role, StringComparison.OrdinalIgnoreCase));

        return result.ToList();
    }

    public async Task<CreateAdminAccountResult> CreateAccountAsync(
        CreateAdminAccountRequest request,
        CancellationToken ct = default)
    {
        var selectedRole = NormalizeManagedRole(request.Role);
        ValidateRequest(request.FullName, request.UserName, request.Password);
        ValidateLegalProfile(selectedRole, request.CitizenId, request.CitizenIdIssuedDate, request.CitizenIdIssuedPlace, request.Address);
        var phoneNumber = VietnamPhoneNumber.NormalizeOrThrow(request.PhoneNumber);
        var officeIds = await ValidateAndNormalizeOfficesAsync(selectedRole, request.OfficeIds, ct);
        await EnsureLoginIdentifiersAvailableAsync(request.UserName, phoneNumber, null, ct);

        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim(),
            FullName = request.FullName.Trim(),
            EmployeeCode = N(request.EmployeeCode),
            PhoneNumber = phoneNumber,
            Email = N(request.Email),
            CitizenId = N(request.CitizenId),
            CitizenIdIssuedDate = request.CitizenIdIssuedDate?.Date,
            CitizenIdIssuedPlace = N(request.CitizenIdIssuedPlace),
            DateOfBirth = request.DateOfBirth?.Date,
            Address = N(request.Address),
            AreaCode = N(request.AreaCode),
            RegistrationStatus = "Approved",
            IsActive = true,
            MustChangePassword = request.MustChangePassword,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = N(request.CreatedByUserId)
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        Ensure(createResult);

        try
        {
            Ensure(await userManager.AddToRoleAsync(user, selectedRole));
            if (selectedRole == AdminRole)
                await ReplaceAdminOfficesAsync(user.Id, officeIds, request.CreatedByUserId, ct);
        }
        catch
        {
            await userManager.DeleteAsync(user);
            throw;
        }

        return new CreateAdminAccountResult { UserId = user.Id, Role = selectedRole };
    }

    public async Task<AdminAccountDetail?> GetDetailAsync(string userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null || user.IsDeleted)
            return null;

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Contains(OwnerRole, StringComparer.OrdinalIgnoreCase))
            return null;

        var role = roles.FirstOrDefault(x =>
            string.Equals(x, AdminRole, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x, VehicleOwnerRole, StringComparison.OrdinalIgnoreCase));
        if (role is null)
            return null;

        await using var db = await factory.CreateDbContextAsync(ct);
        List<AccountOfficeRow> officeRows;
        if (string.Equals(role, AdminRole, StringComparison.OrdinalIgnoreCase))
        {
            officeRows = await db.AdminOffices.AsNoTracking()
                .Where(x => x.AdminUserId == userId && x.IsActive && !x.IsDeleted &&
                            x.CompanyProfile.IsActive && !x.CompanyProfile.IsDeleted)
                .Select(x => new AccountOfficeRow(
                    x.CompanyProfileId,
                    x.CompanyProfile.CompanyName,
                    x.CompanyProfile.BranchName,
                    x.IsPrimary))
                .ToListAsync(ct);
        }
        else
        {
            officeRows = await db.OfficeVehicles.AsNoTracking()
                .Where(x => x.IsActive && !x.IsDeleted && x.AssignedTo == null &&
                            x.Vehicle.AssignedDriverId == userId && !x.Vehicle.IsDeleted &&
                            x.CompanyProfile.IsActive && !x.CompanyProfile.IsDeleted)
                .Select(x => new AccountOfficeRow(
                    x.CompanyProfileId,
                    x.CompanyProfile.CompanyName,
                    x.CompanyProfile.BranchName,
                    x.IsPrimary))
                .ToListAsync(ct);
        }

        var offices = officeRows
            .GroupBy(x => x.CompanyProfileId)
            .Select(x => x.OrderByDescending(y => y.IsPrimary).First())
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.CompanyName)
            .ThenBy(x => x.BranchName)
            .ToList();

        var vehicles = await db.Vehicles.AsNoTracking()
            .Where(x => x.AssignedDriverId == userId)
            .OrderBy(x => x.PlateNumber)
            .Select(x => x.PlateNumber)
            .ToListAsync(ct);

        return new AdminAccountDetail
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            FullName = user.FullName,
            EmployeeCode = user.EmployeeCode,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email,
            Role = role,
            OfficeIds = offices.Select(x => x.CompanyProfileId).ToHashSet(),
            OfficeNames = string.Join(", ", offices.Select(x => OfficeDisplay(x.CompanyName, x.BranchName))),
            CitizenId = user.CitizenId,
            CitizenIdIssuedDate = user.CitizenIdIssuedDate,
            CitizenIdIssuedPlace = user.CitizenIdIssuedPlace,
            DateOfBirth = user.DateOfBirth,
            Address = user.Address,
            AreaCode = user.AreaCode,
            VehicleOwnerSignatureFileUrl = user.VehicleOwnerSignatureFileUrl,
            VehicleOwnerSignedAt = user.VehicleOwnerSignedAt,
            VehicleCount = vehicles.Count,
            VehiclePlates = string.Join(", ", vehicles),
            IsActive = user.IsActive,
            MustChangePassword = user.MustChangePassword,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public async Task<ServiceResult> UpdateAccountAsync(UpdateAdminAccountRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return ServiceResult.Failure("Thiếu mã tài khoản.");

        string selectedRole;
        string phoneNumber;
        HashSet<Guid> officeIds;
        try
        {
            selectedRole = NormalizeManagedRole(request.Role);
            if (string.IsNullOrWhiteSpace(request.FullName))
                throw new InvalidOperationException("Vui lòng nhập họ và tên.");
            ValidateLegalProfile(selectedRole, request.CitizenId, request.CitizenIdIssuedDate, request.CitizenIdIssuedPlace, request.Address);
            phoneNumber = VietnamPhoneNumber.NormalizeOrThrow(request.PhoneNumber);
            officeIds = await ValidateAndNormalizeOfficesAsync(selectedRole, request.OfficeIds, ct);
        }
        catch (InvalidOperationException ex)
        {
            return ServiceResult.Failure(ex.Message);
        }

        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null || user.IsDeleted)
            return ServiceResult.Failure("Không tìm thấy tài khoản.");

        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Contains(OwnerRole, StringComparer.OrdinalIgnoreCase))
            return ServiceResult.Failure("Không được chỉnh tài khoản Owner tại màn hình này.");

        if (selectedRole == AdminRole && currentRoles.Contains(VehicleOwnerRole, StringComparer.OrdinalIgnoreCase))
        {
            await using var checkDb = await factory.CreateDbContextAsync(ct);
            if (await checkDb.Vehicles.AnyAsync(x => x.AssignedDriverId == user.Id, ct))
                return ServiceResult.Failure("Tài khoản đang sở hữu xe. Hãy chuyển các xe sang Chủ xe khác trước khi đổi sang Quản lý.");
        }

        try
        {
            await EnsureLoginIdentifiersAvailableAsync(user.UserName ?? string.Empty, phoneNumber, user.Id, ct);
        }
        catch (InvalidOperationException ex)
        {
            return ServiceResult.Failure(ex.Message);
        }

        user.FullName = request.FullName.Trim();
        user.EmployeeCode = N(request.EmployeeCode);
        user.PhoneNumber = phoneNumber;
        user.Email = N(request.Email);
        user.CitizenId = N(request.CitizenId);
        user.CitizenIdIssuedDate = request.CitizenIdIssuedDate?.Date;
        user.CitizenIdIssuedPlace = N(request.CitizenIdIssuedPlace);
        user.DateOfBirth = request.DateOfBirth?.Date;
        user.Address = N(request.Address);
        user.AreaCode = N(request.AreaCode);
        user.IsActive = request.IsActive;
        user.MustChangePassword = request.MustChangePassword;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedByUserId = N(request.UpdatedByUserId);

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return ServiceResult.Failure(updateResult.Errors.Select(x => x.Description));

        var managedRoles = currentRoles.Where(x =>
            string.Equals(x, AdminRole, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x, VehicleOwnerRole, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (managedRoles.Length > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, managedRoles);
            if (!removeResult.Succeeded)
                return ServiceResult.Failure(removeResult.Errors.Select(x => x.Description));
        }

        var addResult = await userManager.AddToRoleAsync(user, selectedRole);
        if (!addResult.Succeeded)
            return ServiceResult.Failure(addResult.Errors.Select(x => x.Description));

        await ReplaceAdminOfficesAsync(
            user.Id,
            selectedRole == AdminRole ? officeIds : [],
            request.UpdatedByUserId,
            ct);

        if (selectedRole == VehicleOwnerRole)
            await SyncOwnedVehicleSnapshotsAsync(user, request.UpdatedByUserId, ct);

        return ServiceResult.Success(selectedRole == VehicleOwnerRole
            ? "Đã cập nhật tài khoản Chủ xe và đồng bộ thông tin pháp lý sang các xe đang sở hữu."
            : "Đã cập nhật tài khoản Quản lý và phạm vi Công ty/Văn phòng.");
    }

    public async Task<ServiceResult> ResetPasswordToDefaultAsync(string userId, CancellationToken ct = default)
    {
        var user = await FindManagedUserAsync(userId);
        if (user is null)
            return ServiceResult.Failure("Không tìm thấy tài khoản.");

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, DefaultResetPassword);
        if (!result.Succeeded)
            return ServiceResult.Failure(result.Errors.Select(x => x.Description));

        user.MustChangePassword = true;
        user.UpdatedAt = DateTime.UtcNow;
        var update = await userManager.UpdateAsync(user);
        return update.Succeeded
            ? ServiceResult.Success($"Đã reset mật khẩu về {DefaultResetPassword}.")
            : ServiceResult.Failure(update.Errors.Select(x => x.Description));
    }

    public async Task<ServiceResult> SetActiveAsync(string userId, bool isActive, CancellationToken ct = default)
    {
        var user = await FindManagedUserAsync(userId);
        if (user is null)
            return ServiceResult.Failure("Không tìm thấy tài khoản.");

        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded
            ? ServiceResult.Success(isActive ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản.")
            : ServiceResult.Failure(result.Errors.Select(x => x.Description));
    }

    public async Task<ServiceResult> DeleteAsync(
        string userId,
        string? deletedByUserId = null,
        CancellationToken ct = default)
    {
        var user = await FindManagedUserAsync(userId);
        if (user is null)
            return ServiceResult.Failure("Không tìm thấy tài khoản.");

        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.Vehicles.AnyAsync(x => x.AssignedDriverId == userId, ct))
            return ServiceResult.Failure("Tài khoản còn xe đang được gán. Hãy chuyển chủ tài khoản của xe trước khi xóa.");

        user.IsDeleted = true;
        user.IsActive = false;
        user.DeletedAt = DateTime.UtcNow;
        user.DeletedBy = N(deletedByUserId) ?? "OWNER";
        user.UpdatedAt = DateTime.UtcNow;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return ServiceResult.Failure(result.Errors.Select(x => x.Description));

        var assignments = await db.AdminOffices.Where(x => x.AdminUserId == userId).ToListAsync(ct);
        foreach (var assignment in assignments)
        {
            assignment.IsDeleted = true;
            assignment.IsActive = false;
            assignment.DeletedAt = DateTime.UtcNow;
            assignment.DeletedBy = N(deletedByUserId) ?? "OWNER";
        }
        await db.SaveChangesAsync(ct);
        return ServiceResult.Success("Đã xóa mềm tài khoản.");
    }

    private async Task<ApplicationUser?> FindManagedUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;
        var user = await userManager.FindByIdAsync(userId);
        if (user is null || user.IsDeleted)
            return null;
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Contains(OwnerRole, StringComparer.OrdinalIgnoreCase))
            return null;
        return roles.Any(x =>
            string.Equals(x, AdminRole, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x, VehicleOwnerRole, StringComparison.OrdinalIgnoreCase)) ? user : null;
    }

    private async Task<HashSet<Guid>> ValidateAndNormalizeOfficesAsync(
        string selectedRole,
        IEnumerable<Guid>? requestedOfficeIds,
        CancellationToken ct)
    {
        if (selectedRole != AdminRole)
            return [];

        var ids = (requestedOfficeIds ?? [])
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToHashSet();
        if (ids.Count == 0)
            throw new InvalidOperationException("Tài khoản Quản lý phải được chọn ít nhất một Công ty/Văn phòng.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var validIds = await db.CompanyProfiles.AsNoTracking()
            .Where(x => ids.Contains(x.Id) && x.IsActive && !x.IsDeleted)
            .Select(x => x.Id)
            .ToListAsync(ct);
        if (validIds.Count != ids.Count)
            throw new InvalidOperationException("Có Công ty/Văn phòng không tồn tại hoặc đã ngừng hoạt động.");
        return validIds.ToHashSet();
    }

    private async Task ReplaceAdminOfficesAsync(
        string userId,
        HashSet<Guid> officeIds,
        string? actorUserId,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var existing = await db.AdminOffices.IgnoreQueryFilters()
            .Where(x => x.AdminUserId == userId)
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        var orderedIds = await db.CompanyProfiles.AsNoTracking()
            .Where(x => officeIds.Contains(x.Id))
            .OrderBy(x => x.CompanyName)
            .ThenBy(x => x.BranchName)
            .Select(x => x.Id)
            .ToArrayAsync(ct);
        var primaryOfficeId = existing
            .FirstOrDefault(x => officeIds.Contains(x.CompanyProfileId) && x.IsPrimary)
            ?.CompanyProfileId ?? orderedIds.FirstOrDefault();

        foreach (var row in existing)
        {
            if (officeIds.Contains(row.CompanyProfileId))
            {
                row.IsDeleted = false;
                row.DeletedAt = null;
                row.DeletedBy = null;
                row.IsActive = true;
                row.IsPrimary = primaryOfficeId == row.CompanyProfileId;
                row.UpdatedAt = now;
                row.UpdatedBy = actorUserId;
            }
            else
            {
                row.IsDeleted = true;
                row.IsActive = false;
                row.DeletedAt = now;
                row.DeletedBy = N(actorUserId) ?? "OWNER";
                row.UpdatedAt = now;
                row.UpdatedBy = actorUserId;
            }
        }

        var existingIds = existing.Select(x => x.CompanyProfileId).ToHashSet();
        foreach (var officeId in orderedIds.Where(x => !existingIds.Contains(x)))
        {
            db.AdminOffices.Add(new AdminOffice
            {
                AdminUserId = userId,
                CompanyProfileId = officeId,
                IsPrimary = officeId == primaryOfficeId,
                IsActive = true,
                AssignedAt = now,
                AssignedByUserId = N(actorUserId),
                CreatedAt = now,
                CreatedBy = N(actorUserId)
            });
        }
        await db.SaveChangesAsync(ct);
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
            vehicle.OwnerSignatureFileUrl = owner.VehicleOwnerSignatureFileUrl;
            vehicle.OwnerSignatureHash = owner.VehicleOwnerSignatureHash;
            vehicle.OwnerSignedAt = owner.VehicleOwnerSignedAt;
            vehicle.UpdatedAt = now;
            vehicle.UpdatedBy = N(actorUserId);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureLoginIdentifiersAvailableAsync(
        string userName,
        string phoneNumber,
        string? excludedUserId,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var normalizedUserName = userManager.NormalizeName(userName.Trim());
        var normalizedPhoneAsUserName = userManager.NormalizeName(phoneNumber);
        var conflict = await db.Users.AsNoTracking().AnyAsync(x =>
            x.Id != excludedUserId &&
            (x.NormalizedUserName == normalizedUserName ||
             x.NormalizedUserName == normalizedPhoneAsUserName ||
             x.PhoneNumber == phoneNumber), ct);
        if (conflict)
            throw new InvalidOperationException("Tên đăng nhập hoặc số điện thoại đang được sử dụng.");
    }

    private static async Task<Dictionary<string, string>> LoadManagedRoleMapAsync(
        ApplicationDbContext db,
        string[] userIds,
        CancellationToken ct)
    {
        if (userIds.Length == 0)
            return [];
        var rows = await (
            from ur in db.UserRoles.AsNoTracking()
            join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where userIds.Contains(ur.UserId) &&
                  (r.Name == AdminRole || r.Name == VehicleOwnerRole)
            select new { ur.UserId, Role = r.Name! })
            .ToListAsync(ct);
        return rows.GroupBy(x => x.UserId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Role).First());
    }

    private static async Task<Dictionary<string, string>> LoadOfficeNameMapAsync(
        ApplicationDbContext db,
        string[] userIds,
        CancellationToken ct)
    {
        if (userIds.Length == 0)
            return [];

        var adminRows = await db.AdminOffices.AsNoTracking()
            .Where(x => userIds.Contains(x.AdminUserId) && x.IsActive && !x.IsDeleted &&
                        x.CompanyProfile.IsActive && !x.CompanyProfile.IsDeleted)
            .Select(x => new
            {
                UserId = x.AdminUserId,
                x.CompanyProfile.CompanyName,
                x.CompanyProfile.BranchName
            })
            .ToListAsync(ct);

        var vehicleRows = await db.OfficeVehicles.AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted && x.AssignedTo == null && x.Vehicle.AssignedDriverId != null &&
                        userIds.Contains(x.Vehicle.AssignedDriverId) &&
                        x.CompanyProfile.IsActive && !x.CompanyProfile.IsDeleted)
            .Select(x => new
            {
                UserId = x.Vehicle.AssignedDriverId!,
                x.CompanyProfile.CompanyName,
                x.CompanyProfile.BranchName
            })
            .ToListAsync(ct);

        return adminRows.Concat(vehicleRows)
            .GroupBy(x => x.UserId)
            .ToDictionary(
                x => x.Key,
                x => string.Join(", ", x.Select(y => OfficeDisplay(y.CompanyName, y.BranchName)).Distinct().OrderBy(y => y)));
    }

    private sealed record AccountOfficeRow(
        Guid CompanyProfileId,
        string CompanyName,
        string? BranchName,
        bool IsPrimary);

    private static string NormalizeManagedRole(string? role)
    {
        if (string.Equals(role, AdminRole, StringComparison.OrdinalIgnoreCase))
            return AdminRole;
        if (string.Equals(role, VehicleOwnerRole, StringComparison.OrdinalIgnoreCase))
            return VehicleOwnerRole;
        throw new InvalidOperationException("Vai trò chỉ được chọn Quản lý hoặc Chủ xe.");
    }

    private static void ValidateRequest(string fullName, string userName, string password)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new InvalidOperationException("Vui lòng nhập họ và tên.");
        if (string.IsNullOrWhiteSpace(userName))
            throw new InvalidOperationException("Vui lòng nhập tên đăng nhập.");
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Vui lòng nhập mật khẩu.");
    }

    private static void ValidateLegalProfile(
        string selectedRole,
        string? citizenId,
        DateTime? citizenIdIssuedDate,
        string? citizenIdIssuedPlace,
        string? address)
    {
        if (selectedRole != VehicleOwnerRole)
            return;

        if (string.IsNullOrWhiteSpace(citizenId))
            throw new InvalidOperationException("Tài khoản Chủ xe phải có số CCCD.");
        if (!citizenIdIssuedDate.HasValue)
            throw new InvalidOperationException("Tài khoản Chủ xe phải có ngày cấp CCCD.");
        if (string.IsNullOrWhiteSpace(citizenIdIssuedPlace))
            throw new InvalidOperationException("Tài khoản Chủ xe phải có nơi cấp CCCD.");
        if (string.IsNullOrWhiteSpace(address))
            throw new InvalidOperationException("Tài khoản Chủ xe phải có địa chỉ chủ xe.");
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }

    private static string OfficeDisplay(string companyName, string? branchName) =>
        string.IsNullOrWhiteSpace(branchName) ? companyName : $"{companyName} - {branchName}";

    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
