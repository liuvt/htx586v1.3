using HTX586CONTRACT.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HTX586CONTRACT.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    private static readonly string[] Roles = ["Admin", "Driver"];

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

        // Database mới hoàn toàn: tạo schema trực tiếp từ model hiện tại.
        await db.Database.EnsureCreatedAsync(ct);

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                    throw new InvalidOperationException($"Không thể tạo role {role}: {string.Join("; ", result.Errors.Select(x => x.Description))}");
            }
        }

        var userName = configuration["Seed:AdminUserName"]?.Trim();
        var password = configuration["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Chưa cấu hình Seed:AdminUserName và Seed:AdminPassword. Database đã tạo nhưng chưa có tài khoản Admin.");
            return;
        }

        var admin = await userManager.FindByNameAsync(userName);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = userName,
                FullName = configuration["Seed:AdminFullName"]?.Trim() ?? "Quản trị viên",
                PhoneNumber = configuration["Seed:AdminPhoneNumber"]?.Trim(),
                Email = configuration["Seed:AdminEmail"]?.Trim(),
                CompanyName = configuration["Seed:CompanyName"]?.Trim(),
                CompanyAddress = configuration["Seed:CompanyAddress"]?.Trim(),
                CompanyTaxCode = configuration["Seed:CompanyTaxCode"]?.Trim(),
                CompanyRepresentativeName = configuration["Seed:CompanyRepresentativeName"]?.Trim(),
                CompanyRepresentativePosition = configuration["Seed:CompanyRepresentativePosition"]?.Trim(),
                RegistrationStatus = "Approved",
                IsActive = true,
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await userManager.CreateAsync(admin, password);
            if (!createResult.Succeeded)
                throw new InvalidOperationException($"Không thể tạo Admin: {string.Join("; ", createResult.Errors.Select(x => x.Description))}");
        }

        if (!await userManager.IsInRoleAsync(admin, "Admin"))
        {
            var roleResult = await userManager.AddToRoleAsync(admin, "Admin");
            if (!roleResult.Succeeded)
                throw new InvalidOperationException($"Không thể gán role Admin: {string.Join("; ", roleResult.Errors.Select(x => x.Description))}");
        }
    }
}
