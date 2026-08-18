using System.Security.Claims;
using HTX586CONTRACT.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace HTX586CONTRACT.Infrastructure.Identity;

/// <summary>
/// Executes ASP.NET Core Identity operations inside a short-lived DI scope.
///
/// IMPORTANT:
/// Never attach an ApplicationUser instance that was loaded by another scope/DbContext
/// to the UserManager in the new scope. ASP.NET Identity validators may query the same
/// user again (for example by normalized username) before UpdateAsync is persisted.
/// Passing the detached instance directly can therefore produce:
/// "another instance with the same key value is already being tracked".
///
/// All operations below reload the user inside the SAME scope that performs the write.
/// </summary>
public sealed class SafeUserManager(IServiceScopeFactory scopeFactory)
{
    public async Task<IdentityResult> CreateAsync(ApplicationUser user, string password)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await manager.CreateAsync(user, password);
    }

    public async Task<IdentityResult> AddToRoleAsync(ApplicationUser user, string role)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var managed = await manager.FindByIdAsync(user.Id);
        if (managed is null)
            return UserNotFound(user.Id);

        return await manager.AddToRoleAsync(managed, role);
    }

    public async Task<IdentityResult> DeleteAsync(ApplicationUser user)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var managed = await manager.FindByIdAsync(user.Id);
        if (managed is null)
            return UserNotFound(user.Id);

        return await manager.DeleteAsync(managed);
    }

    public async Task<ApplicationUser?> FindByIdAsync(string userId)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await manager.FindByIdAsync(userId);
    }

    public async Task<IList<string>> GetRolesAsync(ApplicationUser user)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var managed = await manager.FindByIdAsync(user.Id);
        return managed is null ? Array.Empty<string>() : await manager.GetRolesAsync(managed);
    }

    public async Task<bool> IsInRoleAsync(ApplicationUser user, string role)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var managed = await manager.FindByIdAsync(user.Id);
        return managed is not null && await manager.IsInRoleAsync(managed, role);
    }

    /// <summary>
    /// Updates profile/application fields without ever attaching the caller's detached
    /// ApplicationUser instance to the new DbContext. PasswordHash, normalized fields and
    /// ConcurrencyStamp are deliberately preserved from the freshly loaded database row.
    /// This is especially important after ResetPasswordAsync, where the caller still holds
    /// a stale PasswordHash/SecurityStamp snapshot.
    /// </summary>
    public async Task<IdentityResult> UpdateAsync(ApplicationUser user, bool copySecurityStamp = false)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var managed = await manager.FindByIdAsync(user.Id);
        if (managed is null)
            return UserNotFound(user.Id);

        CopyMutableValues(user, managed, copySecurityStamp);
        return await manager.UpdateAsync(managed);
    }

    public async Task<IdentityResult> RemoveFromRolesAsync(ApplicationUser user, IEnumerable<string> roles)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var managed = await manager.FindByIdAsync(user.Id);
        if (managed is null)
            return UserNotFound(user.Id);

        return await manager.RemoveFromRolesAsync(managed, roles);
    }

    public async Task<string> GeneratePasswordResetTokenAsync(ApplicationUser user)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var managed = await manager.FindByIdAsync(user.Id)
            ?? throw new InvalidOperationException($"Không tìm thấy tài khoản '{user.Id}'.");

        return await manager.GeneratePasswordResetTokenAsync(managed);
    }

    public async Task<IdentityResult> ResetPasswordAsync(ApplicationUser user, string token, string newPassword)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var managed = await manager.FindByIdAsync(user.Id);
        if (managed is null)
            return UserNotFound(user.Id);

        return await manager.ResetPasswordAsync(managed, token, newPassword);
    }

    public async Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await manager.GetUserAsync(principal);
    }

    /// <summary>
    /// Matches ASP.NET Core Identity's default UpperInvariantLookupNormalizer without
    /// touching the UserManager/DbContext. This is safe to call from import validation.
    /// </summary>
    public string? NormalizeName(string? name)
        => name?.Normalize().ToUpperInvariant();

    private static IdentityResult UserNotFound(string userId) =>
        IdentityResult.Failed(new IdentityError
        {
            Code = "UserNotFound",
            Description = $"Không tìm thấy tài khoản '{userId}'."
        });

    private static void CopyMutableValues(
        ApplicationUser source,
        ApplicationUser target,
        bool copySecurityStamp)
    {
        // Identity profile fields. Never copy PasswordHash, ConcurrencyStamp,
        // NormalizedUserName or NormalizedEmail from a detached/stale instance.
        target.UserName = source.UserName;
        target.Email = source.Email;
        target.PhoneNumber = source.PhoneNumber;

        // Authentication/lockout counters are intentionally NOT copied from the
        // detached snapshot. They may have changed after the snapshot was read.
        // Keeping the freshly loaded values avoids overwriting concurrent sign-in state.

        if (copySecurityStamp)
            target.SecurityStamp = source.SecurityStamp;

        // Application fields.
        target.FullName = source.FullName;
        target.EmployeeCode = source.EmployeeCode;
        target.CitizenId = source.CitizenId;
        target.CitizenIdIssuedDate = source.CitizenIdIssuedDate;
        target.CitizenIdIssuedPlace = source.CitizenIdIssuedPlace;
        target.DateOfBirth = source.DateOfBirth;
        target.Address = source.Address;
        target.AreaCode = source.AreaCode;
        target.AvatarUrl = source.AvatarUrl;
        target.CitizenIdFrontUrl = source.CitizenIdFrontUrl;
        target.CitizenIdBackUrl = source.CitizenIdBackUrl;
        target.DriverLicenseNumber = source.DriverLicenseNumber;
        target.DriverLicenseClass = source.DriverLicenseClass;
        target.DriverLicenseIssuedDate = source.DriverLicenseIssuedDate;
        target.DriverLicenseExpiryDate = source.DriverLicenseExpiryDate;
        target.DriverLicenseFrontUrl = source.DriverLicenseFrontUrl;
        target.DriverLicenseBackUrl = source.DriverLicenseBackUrl;

        target.DriverSignatureFileUrl = source.DriverSignatureFileUrl;
        target.DriverSignatureHash = source.DriverSignatureHash;
        target.DriverSignedAt = source.DriverSignedAt;
        target.DriverSignatureIsActive = source.DriverSignatureIsActive;
        target.DriverSignatureInactiveAt = source.DriverSignatureInactiveAt;

        target.VehicleOwnerSignatureFileUrl = source.VehicleOwnerSignatureFileUrl;
        target.VehicleOwnerSignatureHash = source.VehicleOwnerSignatureHash;
        target.VehicleOwnerSignedAt = source.VehicleOwnerSignedAt;

        target.RegistrationStatus = source.RegistrationStatus;
        target.RegistrationRequestedAt = source.RegistrationRequestedAt;
        target.RegistrationViewedAt = source.RegistrationViewedAt;
        target.RegistrationViewedByUserId = source.RegistrationViewedByUserId;
        target.RegistrationReviewedAt = source.RegistrationReviewedAt;
        target.RegistrationReviewedByUserId = source.RegistrationReviewedByUserId;
        target.RegistrationReviewNote = source.RegistrationReviewNote;

        target.IsActive = source.IsActive;
        target.MustChangePassword = source.MustChangePassword;
        target.CreatedAt = source.CreatedAt;
        target.CreatedByUserId = source.CreatedByUserId;
        target.UpdatedAt = source.UpdatedAt;
        target.UpdatedByUserId = source.UpdatedByUserId;
        target.IsDeleted = source.IsDeleted;
        target.DeletedAt = source.DeletedAt;
        target.DeletedBy = source.DeletedBy;
    }
}
