using HTX586CONTRACT.Domain.Companies;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HTX586CONTRACT.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var factory = provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = provider.GetRequiredService<IConfiguration>();

        await using var db = await factory.CreateDbContextAsync();

        // Ver6 dùng current model + SQL nâng cấp idempotent, không gọi MigrateAsync
        // để tránh model snapshot cũ chặn ứng dụng khi khởi động.
        await db.Database.EnsureCreatedAsync();
        await DatabaseSchemaInitializer.ApplyAsync(db);

        await SeedRolesAsync(roleManager);
        await SeedAdminAsync(userManager, configuration);
        await SeedCompaniesAsync(db);
        await SeedContractTypesAsync(db);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in new[] { "Admin", "Driver" })
        {
            if (await roleManager.RoleExistsAsync(role)) continue;
            Ensure(await roleManager.CreateAsync(new IdentityRole(role)), $"Không thể tạo quyền {role}");
        }
    }

    private static async Task SeedAdminAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        var userName = configuration["Seed:AdminUserName"]?.Trim();
        if (string.IsNullOrWhiteSpace(userName))
            userName = "admin";

        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            var password = configuration["Seed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException(
                    "Database chưa có tài khoản quản trị. Hãy cấu hình Seed:AdminPassword " +
                    "bằng user-secrets hoặc biến môi trường Seed__AdminPassword rồi chạy lại ứng dụng.");

            user = new ApplicationUser
            {
                UserName = userName,
                FullName = "Quản trị hệ thống",
                EmployeeCode = "ADMIN",
                IsActive = true,
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow
            };
            Ensure(await userManager.CreateAsync(user, password), "Không thể tạo tài khoản admin");
        }

        if (!await userManager.IsInRoleAsync(user, "Admin"))
            Ensure(await userManager.AddToRoleAsync(user, "Admin"), "Không thể gán quyền Admin");
    }

    private static async Task SeedCompaniesAsync(ApplicationDbContext db)
    {
        var seeds = new[]
        {
            new CompanyProfile
            {
                CompanyName = "HỢP TÁC XÃ VẬN TẢI 586 - CẦN THƠ",
                BranchName = "Văn phòng đại diện Cần Thơ",
                TaxCode = "1801774247",
                BusinessLicenseNumber = "92240166/GPKDVT",
                Address = "Khu dân cư lô số 11B - KĐT Nam Cần Thơ, Phường Cái Răng, Thành phố Cần Thơ",
                PhoneNumber = "0920365507",
                RepresentativeName = "Nguyễn Việt Kiều Anh",
                RepresentativePosition = "Chủ tịch Hội đồng quản trị",
                RepresentativeCitizenId = "092195007693",
                RepresentativeCitizenIdIssuedDate = new DateTime(2021, 8, 14),
                RepresentativeCitizenIdIssuedPlace = "Cục Cảnh sát quản lý hành chính về trật tự xã hội",
                IsActive = true
            }
        };

        foreach (var seed in seeds)
        {
            var existing = await db.CompanyProfiles.FirstOrDefaultAsync(x => x.TaxCode == seed.TaxCode);
            if (existing is null)
            {
                db.CompanyProfiles.Add(seed);
                continue;
            }

            existing.CompanyName = seed.CompanyName;
            existing.BranchName = seed.BranchName;
            existing.BusinessLicenseNumber = seed.BusinessLicenseNumber;
            existing.Address = seed.Address;
            existing.PhoneNumber = seed.PhoneNumber;
            existing.RepresentativeName = seed.RepresentativeName;
            existing.RepresentativePosition = seed.RepresentativePosition;
            existing.RepresentativeCitizenId = seed.RepresentativeCitizenId;
            existing.RepresentativeCitizenIdIssuedDate = seed.RepresentativeCitizenIdIssuedDate;
            existing.RepresentativeCitizenIdIssuedPlace = seed.RepresentativeCitizenIdIssuedPlace;
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedContractTypesAsync(ApplicationDbContext db)
    {
        var types = new[]
        {
            (Code: "DRIVER", Name: "Hợp đồng tài xế"),
            (Code: "CARGO", Name: "Hợp đồng vận chuyển hàng hóa"),
            (Code: "LONG_DISTANCE", Name: "Hợp đồng vận chuyển đường dài")
        };

        foreach (var item in types)
        {
            var type = await db.ContractTypes.FirstOrDefaultAsync(x => x.Code == item.Code);
            if (type is null)
            {
                type = new ContractType
                {
                    Id = Guid.NewGuid(),
                    Code = item.Code,
                    Name = item.Name,
                    IsActive = true,
                    RequireCustomerSignature = true,
                    RequireDriverSignature = true,
                    RequireLocation = true,
                    CreatedAt = DateTime.UtcNow
                };
                db.ContractTypes.Add(type);
            }
            else
            {
                type.Name = item.Name;
                type.IsActive = true;
            }

            await db.SaveChangesAsync();

            var template = await db.ContractTemplates.FirstOrDefaultAsync(x => x.ContractTypeId == type.Id && x.IsActive);
            if (template is null)
            {
                db.ContractTemplates.Add(new ContractTemplate
                {
                    Id = Guid.NewGuid(),
                    ContractTypeId = type.Id,
                    Name = $"Mẫu {item.Name}",
                    Version = 1,
                    HtmlContent = item.Name,
                    IsActive = true,
                    EffectiveFrom = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
        }
    }

    private static void Ensure(IdentityResult result, string message)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"{message}: {string.Join("; ", result.Errors.Select(x => x.Description))}");
    }
}
