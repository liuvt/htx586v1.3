using System.Globalization;
using HTX586CONTRACT.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HTX586CONTRACT.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    private const string AdminRole = "Admin";
    private static readonly string[] Roles = [AdminRole, "Driver"];

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseInitializer");

        await db.Database.MigrateAsync(ct);
        await SeedRolesAsync(roleManager);
        await SeedMainAdminAsync(userManager, configuration, logger, ct);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in Roles)
        {
            if (await roleManager.RoleExistsAsync(role))
                continue;

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            EnsureSucceeded(result, $"Không thể tạo role {role}");
        }
    }

    private static async Task SeedMainAdminAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken ct)
    {
        var seed = AdminSeedData.FromConfiguration(configuration);
        if (string.IsNullOrWhiteSpace(seed.UserName) || string.IsNullOrWhiteSpace(seed.Password))
        {
            logger.LogWarning(
                "Chưa cấu hình Seed:AdminUserName và Seed:AdminPassword. " +
                "Database đã được cập nhật nhưng chưa có tài khoản Admin chính.");
            return;
        }

        var admin = await userManager.FindByNameAsync(seed.UserName);
        var wasCreated = admin is null;

        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = seed.UserName,
                CreatedAt = DateTime.UtcNow
            };

            ApplySeedData(admin, seed, overwriteExisting: true);

            var createResult = await userManager.CreateAsync(admin, seed.Password);
            EnsureSucceeded(createResult, "Không thể tạo tài khoản Admin chính");
        }
        else
        {
            var changed = ApplySeedData(admin, seed, seed.OverwriteExistingProfile);

            // Khôi phục trạng thái hợp lệ cho tài khoản Admin chính nếu dữ liệu cũ bị vô hiệu hóa.
            if (!admin.IsActive)
            {
                admin.IsActive = true;
                changed = true;
            }

            if (admin.IsDeleted)
            {
                admin.IsDeleted = false;
                admin.DeletedAt = null;
                admin.DeletedBy = null;
                changed = true;
            }

            if (!string.Equals(admin.RegistrationStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                admin.RegistrationStatus = "Approved";
                changed = true;
            }

            if (changed)
            {
                admin.UpdatedAt = DateTime.UtcNow;
                var updateResult = await userManager.UpdateAsync(admin);
                EnsureSucceeded(updateResult, "Không thể cập nhật tài khoản Admin chính");
            }
        }

        if (!await userManager.IsInRoleAsync(admin, AdminRole))
        {
            var roleResult = await userManager.AddToRoleAsync(admin, AdminRole);
            EnsureSucceeded(roleResult, "Không thể gán role Admin cho tài khoản chính");
        }

        logger.LogInformation(
            wasCreated
                ? "Đã tạo tài khoản Admin chính '{AdminUserName}' và hồ sơ công ty."
                : "Đã kiểm tra/cập nhật tài khoản Admin chính '{AdminUserName}' và hồ sơ công ty.",
            admin.UserName);

        ct.ThrowIfCancellationRequested();
    }

    private static bool ApplySeedData(
        ApplicationUser admin,
        AdminSeedData seed,
        bool overwriteExisting)
    {
        var changed = false;

        changed |= SetValue(admin.FullName, seed.FullName, overwriteExisting, value => admin.FullName = value);
        changed |= SetValue(admin.EmployeeCode, seed.EmployeeCode, overwriteExisting, value => admin.EmployeeCode = value);
        changed |= SetValue(admin.PhoneNumber, seed.AdminPhoneNumber, overwriteExisting, value => admin.PhoneNumber = value);
        changed |= SetValue(admin.Email, seed.AdminEmail, overwriteExisting, value => admin.Email = value);

        changed |= SetValue(admin.CompanyName, seed.CompanyName, overwriteExisting, value => admin.CompanyName = value);
        changed |= SetValue(admin.CompanyBranchName, seed.CompanyBranchName, overwriteExisting, value => admin.CompanyBranchName = value);
        changed |= SetValue(admin.CompanyTaxCode, seed.CompanyTaxCode, overwriteExisting, value => admin.CompanyTaxCode = value);
        changed |= SetValue(admin.CompanyBusinessLicenseNumber, seed.CompanyBusinessLicenseNumber, overwriteExisting, value => admin.CompanyBusinessLicenseNumber = value);
        changed |= SetValue(admin.CompanyAddress, seed.CompanyAddress, overwriteExisting, value => admin.CompanyAddress = value);
        changed |= SetValue(admin.CompanyPhoneNumber, seed.CompanyPhoneNumber, overwriteExisting, value => admin.CompanyPhoneNumber = value);
        changed |= SetValue(admin.CompanyEmail, seed.CompanyEmail, overwriteExisting, value => admin.CompanyEmail = value);
        changed |= SetValue(admin.CompanyRepresentativeName, seed.CompanyRepresentativeName, overwriteExisting, value => admin.CompanyRepresentativeName = value);
        changed |= SetValue(admin.CompanyRepresentativePosition, seed.CompanyRepresentativePosition, overwriteExisting, value => admin.CompanyRepresentativePosition = value);
        changed |= SetValue(admin.CompanyRepresentativeCitizenId, seed.CompanyRepresentativeCitizenId, overwriteExisting, value => admin.CompanyRepresentativeCitizenId = value);
        changed |= SetValue(admin.CompanyRepresentativeCitizenIdIssuedPlace, seed.CompanyRepresentativeCitizenIdIssuedPlace, overwriteExisting, value => admin.CompanyRepresentativeCitizenIdIssuedPlace = value);

        if (seed.CompanyRepresentativeCitizenIdIssuedDate.HasValue &&
            (overwriteExisting || !admin.CompanyRepresentativeCitizenIdIssuedDate.HasValue) &&
            admin.CompanyRepresentativeCitizenIdIssuedDate != seed.CompanyRepresentativeCitizenIdIssuedDate)
        {
            admin.CompanyRepresentativeCitizenIdIssuedDate = seed.CompanyRepresentativeCitizenIdIssuedDate;
            changed = true;
        }

        if (admin.MustChangePassword != seed.MustChangePassword)
        {
            admin.MustChangePassword = seed.MustChangePassword;
            changed = true;
        }

        if (!admin.IsActive)
        {
            admin.IsActive = true;
            changed = true;
        }

        if (!string.Equals(admin.RegistrationStatus, "Approved", StringComparison.Ordinal))
        {
            admin.RegistrationStatus = "Approved";
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(admin.PhoneNumber) && !admin.PhoneNumberConfirmed)
        {
            admin.PhoneNumberConfirmed = true;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(admin.Email) && !admin.EmailConfirmed)
        {
            admin.EmailConfirmed = true;
            changed = true;
        }

        return changed;
    }

    private static bool SetValue(
        string? currentValue,
        string? seedValue,
        bool overwriteExisting,
        Action<string> setter)
    {
        var normalizedSeed = Normalize(seedValue);
        if (normalizedSeed is null)
            return false;

        if (!overwriteExisting && !string.IsNullOrWhiteSpace(currentValue))
            return false;

        if (string.Equals(currentValue, normalizedSeed, StringComparison.Ordinal))
            return false;

        setter(normalizedSeed);
        return true;
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (result.Succeeded)
            return;

        throw new InvalidOperationException(
            $"{message}: {string.Join("; ", result.Errors.Select(x => x.Description))}");
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record AdminSeedData(
        string UserName,
        string Password,
        string FullName,
        string? EmployeeCode,
        string? AdminPhoneNumber,
        string? AdminEmail,
        bool MustChangePassword,
        bool OverwriteExistingProfile,
        string? CompanyName,
        string? CompanyBranchName,
        string? CompanyTaxCode,
        string? CompanyBusinessLicenseNumber,
        string? CompanyAddress,
        string? CompanyPhoneNumber,
        string? CompanyEmail,
        string? CompanyRepresentativeName,
        string? CompanyRepresentativePosition,
        string? CompanyRepresentativeCitizenId,
        DateTime? CompanyRepresentativeCitizenIdIssuedDate,
        string? CompanyRepresentativeCitizenIdIssuedPlace)
    {
        public static AdminSeedData FromConfiguration(IConfiguration configuration)
        {
            var issuedDateText = configuration["Seed:CompanyRepresentativeCitizenIdIssuedDate"];
            DateTime? issuedDate = null;

            if (!string.IsNullOrWhiteSpace(issuedDateText))
            {
                var acceptedFormats = new[] { "yyyy-MM-dd", "dd/MM/yyyy" };
                if (!DateTime.TryParseExact(
                        issuedDateText.Trim(),
                        acceptedFormats,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var parsedDate))
                {
                    throw new InvalidOperationException(
                        "Seed:CompanyRepresentativeCitizenIdIssuedDate phải theo định dạng yyyy-MM-dd hoặc dd/MM/yyyy.");
                }

                issuedDate = parsedDate.Date;
            }

            return new AdminSeedData(
                UserName: configuration["Seed:AdminUserName"]?.Trim() ?? string.Empty,
                Password: configuration["Seed:AdminPassword"] ?? string.Empty,
                FullName: configuration["Seed:AdminFullName"]?.Trim() ?? "Quản trị viên",
                EmployeeCode: Normalize(configuration["Seed:AdminEmployeeCode"]),
                AdminPhoneNumber: Normalize(configuration["Seed:AdminPhoneNumber"]),
                AdminEmail: Normalize(configuration["Seed:AdminEmail"]),
                MustChangePassword: GetBoolean(configuration["Seed:AdminMustChangePassword"], defaultValue: false),
                OverwriteExistingProfile: GetBoolean(configuration["Seed:OverwriteExistingAdminProfile"], defaultValue: false),
                CompanyName: Normalize(configuration["Seed:CompanyName"]),
                CompanyBranchName: Normalize(configuration["Seed:CompanyBranchName"]),
                CompanyTaxCode: Normalize(configuration["Seed:CompanyTaxCode"]),
                CompanyBusinessLicenseNumber: Normalize(configuration["Seed:CompanyBusinessLicenseNumber"]),
                CompanyAddress: Normalize(configuration["Seed:CompanyAddress"]),
                CompanyPhoneNumber: Normalize(configuration["Seed:CompanyPhoneNumber"]),
                CompanyEmail: Normalize(configuration["Seed:CompanyEmail"]),
                CompanyRepresentativeName: Normalize(configuration["Seed:CompanyRepresentativeName"]),
                CompanyRepresentativePosition: Normalize(configuration["Seed:CompanyRepresentativePosition"]),
                CompanyRepresentativeCitizenId: Normalize(configuration["Seed:CompanyRepresentativeCitizenId"]),
                CompanyRepresentativeCitizenIdIssuedDate: issuedDate,
                CompanyRepresentativeCitizenIdIssuedPlace: Normalize(configuration["Seed:CompanyRepresentativeCitizenIdIssuedPlace"]));
        }

        private static bool GetBoolean(string? value, bool defaultValue) =>
            bool.TryParse(value, out var result) ? result : defaultValue;
    }
}
