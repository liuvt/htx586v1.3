using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HTX586CONTRACT.Infrastructure.Persistence;

/// <summary>
/// Khởi tạo database mới. Dự án không còn chạy các script vá schema cũ hoặc
/// tạo dữ liệu demo; toàn bộ cấu trúc được quản lý bằng EF Core Migration.
/// </summary>
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
        await db.Database.MigrateAsync();

        await SeedRolesAsync(roleManager);
        await SeedOwnerAsync(userManager, configuration);
        await SeedContractTypesAsync(db);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in new[] { "Owner", "Admin", "VehicleOwner" })
        {
            if (await roleManager.RoleExistsAsync(role))
                continue;

            Ensure(await roleManager.CreateAsync(new IdentityRole(role)), $"Không thể tạo quyền {role}");
        }
    }

    private static async Task SeedOwnerAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        var userName = configuration["Seed:OwnerUserName"]?.Trim();
        if (string.IsNullOrWhiteSpace(userName))
            userName = "owner";

        var password = configuration["Seed:OwnerPassword"];
        if (string.IsNullOrWhiteSpace(password))
            password = "Htx@586";

        var owner = await userManager.FindByNameAsync(userName);
        if (owner is null)
        {
            owner = new ApplicationUser
            {
                UserName = userName,
                FullName = configuration["Seed:OwnerFullName"]?.Trim() ?? "Owner HTX586",
                EmployeeCode = configuration["Seed:OwnerEmployeeCode"]?.Trim() ?? "OWNER001",
                PhoneNumber = configuration["Seed:OwnerPhoneNumber"]?.Trim() ?? "0900000586",
                Email = Normalize(configuration["Seed:OwnerEmail"]),
                RegistrationStatus = "Approved",
                IsActive = true,
                MustChangePassword = false,
                CreatedByUserId = "SYSTEM_SEED",
                CreatedAt = DateTime.UtcNow
            };

            Ensure(await userManager.CreateAsync(owner, password), "Không thể tạo tài khoản Owner seed");
        }

        if (owner.IsDeleted)
            throw new InvalidOperationException($"Tài khoản Owner seed '{userName}' đã bị xóa mềm.");

        var currentRoles = await userManager.GetRolesAsync(owner);
        var rolesToRemove = currentRoles
            .Where(x => !string.Equals(x, "Owner", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (rolesToRemove.Length > 0)
            Ensure(await userManager.RemoveFromRolesAsync(owner, rolesToRemove), "Không thể chuẩn hóa quyền Owner");

        if (!await userManager.IsInRoleAsync(owner, "Owner"))
            Ensure(await userManager.AddToRoleAsync(owner, "Owner"), "Không thể gán quyền Owner");

        owner.IsActive = true;
        owner.RegistrationStatus = "Approved";
        owner.UpdatedAt = DateTime.UtcNow;
        Ensure(await userManager.UpdateAsync(owner), "Không thể cập nhật tài khoản Owner");
    }

    private static async Task SeedContractTypesAsync(ApplicationDbContext db)
    {
        var passenger = await UpsertContractTypeAsync(
            db,
            "PASSENGER",
            "HỢP ĐỒNG VẬN CHUYỂN HÀNH KHÁCH",
            "Hợp đồng vận chuyển hành khách.");

        var cargo = await UpsertContractTypeAsync(
            db,
            "CARGO",
            "HỢP ĐỒNG VẬN CHUYỂN HÀNG HÓA",
            "Hợp đồng vận chuyển hàng hóa.");

        await db.SaveChangesAsync();
        await EnsureContractTemplateAsync(db, passenger, "Mẫu HỢP ĐỒNG VẬN CHUYỂN HÀNH KHÁCH");
        await EnsureContractTemplateAsync(db, cargo, "Mẫu HỢP ĐỒNG VẬN CHUYỂN HÀNG HÓA");
    }

    private static async Task<ContractType> UpsertContractTypeAsync(
        ApplicationDbContext db,
        string code,
        string name,
        string description)
    {
        var type = await db.ContractTypes.FirstOrDefaultAsync(x => x.Code == code);
        if (type is null)
        {
            type = new ContractType
            {
                Id = Guid.NewGuid(),
                Code = code,
                CreatedAt = DateTime.UtcNow
            };
            db.ContractTypes.Add(type);
        }

        type.Name = name;
        type.Description = description;
        type.IsActive = true;
        type.RequireCustomerSignature = true;
        type.RequireDriverSignature = true;
        type.RequireLocation = true;
        type.UpdatedAt = DateTime.UtcNow;
        return type;
    }

    private static async Task EnsureContractTemplateAsync(
        ApplicationDbContext db,
        ContractType type,
        string name)
    {
        var templates = await db.ContractTemplates
            .Where(x => x.ContractTypeId == type.Id)
            .OrderByDescending(x => x.Version)
            .ToListAsync();

        var template = templates.FirstOrDefault(x => x.IsActive) ?? templates.FirstOrDefault();
        if (template is null)
        {
            template = new ContractTemplate
            {
                Id = Guid.NewGuid(),
                ContractTypeId = type.Id,
                Name = name,
                Version = 1,
                HtmlContent = type.Name,
                IsActive = true,
                EffectiveFrom = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            db.ContractTemplates.Add(template);
        }
        else
        {
            template.Name = name;
            template.IsActive = true;
            template.UpdatedAt = DateTime.UtcNow;
        }

        foreach (var other in templates.Where(x => x.Id != template.Id))
        {
            other.IsActive = false;
            other.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    private static void Ensure(IdentityResult result, string message)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"{message}: {string.Join("; ", result.Errors.Select(x => x.Description))}");
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
