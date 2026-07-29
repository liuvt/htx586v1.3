using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HTX586CONTRACT.Domain.Companies;
using HTX586CONTRACT.Domain.Customers;
using HTX586CONTRACT.Domain.Enums;
using HTX586CONTRACT.Domain.Vehicles;

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

        // Schema được tạo trực tiếp từ Entity + Fluent API bằng EnsureCreatedAsync.
        // Không chạy SQL nâng cấp rời trong thư mục database/*.sql nữa.
        // Nếu cần nâng cấp database cũ đã có dữ liệu, hãy dùng EF Core Migration hoặc script chuyển đổi riêng một lần.
        await db.Database.EnsureCreatedAsync();
        await EnsureSoftDeleteColumnsAsync(db);
        await EnsureSimplifiedContractSchemaAsync(db);
        await EnsureDriverRegistrationColumnsAsync(db);

        await SeedRolesAsync(roleManager);

        // Giữ dữ liệu CompanyProfile cũ để chuyển đổi một lần; giao diện mới không còn quản lý bảng này.
        await SeedCompanyProfileAsync(db);
        await SeedDefaultAdminAsync(db, userManager, configuration);
        await RemoveLegacyOwnerRoleAsync(db);
        await BackfillAdminOwnershipAsync(db);

        await SeedContractTypesAsync(db);

        if (configuration.GetValue<bool>("Seed:DemoDataEnabled"))
            await SeedDemoDataAsync(db, userManager, configuration);

        await BackfillContractSnapshotsAsync(db);
    }


    /// <summary>
    /// Nâng cấp tại chỗ database được tạo bằng EnsureCreated ở các phiên bản cũ.
    /// Các bảng CompanyProfiles/Customers và khóa ngoại cũ vẫn được giữ để đọc lịch sử,
    /// nhưng luồng mới dùng AdminId và snapshot trong Contracts.
    /// </summary>
    private static async Task EnsureSimplifiedContractSchemaAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('AspNetUsers','AdminId') IS NULL ALTER TABLE AspNetUsers ADD AdminId nvarchar(450) NULL;
IF COL_LENGTH('AspNetUsers','CompanyName') IS NULL ALTER TABLE AspNetUsers ADD CompanyName nvarchar(300) NULL;
IF COL_LENGTH('AspNetUsers','CompanyBranchName') IS NULL ALTER TABLE AspNetUsers ADD CompanyBranchName nvarchar(300) NULL;
IF COL_LENGTH('AspNetUsers','CompanyTaxCode') IS NULL ALTER TABLE AspNetUsers ADD CompanyTaxCode nvarchar(50) NULL;
IF COL_LENGTH('AspNetUsers','CompanyBusinessLicenseNumber') IS NULL ALTER TABLE AspNetUsers ADD CompanyBusinessLicenseNumber nvarchar(100) NULL;
IF COL_LENGTH('AspNetUsers','CompanyAddress') IS NULL ALTER TABLE AspNetUsers ADD CompanyAddress nvarchar(500) NULL;
IF COL_LENGTH('AspNetUsers','CompanyPhoneNumber') IS NULL ALTER TABLE AspNetUsers ADD CompanyPhoneNumber nvarchar(20) NULL;
IF COL_LENGTH('AspNetUsers','CompanyEmail') IS NULL ALTER TABLE AspNetUsers ADD CompanyEmail nvarchar(256) NULL;
IF COL_LENGTH('AspNetUsers','CompanyRepresentativeName') IS NULL ALTER TABLE AspNetUsers ADD CompanyRepresentativeName nvarchar(200) NULL;
IF COL_LENGTH('AspNetUsers','CompanyRepresentativePosition') IS NULL ALTER TABLE AspNetUsers ADD CompanyRepresentativePosition nvarchar(100) NULL;
IF COL_LENGTH('AspNetUsers','CompanyRepresentativeCitizenId') IS NULL ALTER TABLE AspNetUsers ADD CompanyRepresentativeCitizenId nvarchar(30) NULL;
IF COL_LENGTH('AspNetUsers','CompanyRepresentativeCitizenIdIssuedDate') IS NULL ALTER TABLE AspNetUsers ADD CompanyRepresentativeCitizenIdIssuedDate date NULL;
IF COL_LENGTH('AspNetUsers','CompanyRepresentativeCitizenIdIssuedPlace') IS NULL ALTER TABLE AspNetUsers ADD CompanyRepresentativeCitizenIdIssuedPlace nvarchar(300) NULL;
IF COL_LENGTH('AspNetUsers','CompanySignatureFileUrl') IS NULL ALTER TABLE AspNetUsers ADD CompanySignatureFileUrl nvarchar(500) NULL;
IF COL_LENGTH('AspNetUsers','CompanySignatureHash') IS NULL ALTER TABLE AspNetUsers ADD CompanySignatureHash nvarchar(128) NULL;
IF COL_LENGTH('AspNetUsers','CompanySignedAt') IS NULL ALTER TABLE AspNetUsers ADD CompanySignedAt datetime2 NULL;

IF OBJECT_ID(N'[dbo].[Vehicles]', N'U') IS NOT NULL AND COL_LENGTH('Vehicles','AdminId') IS NULL
    ALTER TABLE Vehicles ADD AdminId nvarchar(450) NULL;

-- Luồng mới không gán xe cố định cho tài xế; dỡ các trigger/index ràng buộc của phiên bản cũ.
IF OBJECT_ID(N'[dbo].[TR_AspNetUsers_ReleaseAssignedVehicle]', N'TR') IS NOT NULL DROP TRIGGER [dbo].[TR_AspNetUsers_ReleaseAssignedVehicle];
IF OBJECT_ID(N'[dbo].[TR_Vehicles_ValidateAssignedDriver]', N'TR') IS NOT NULL DROP TRIGGER [dbo].[TR_Vehicles_ValidateAssignedDriver];
IF OBJECT_ID(N'[dbo].[TR_AspNetUserRoles_ReleaseAssignedVehicle]', N'TR') IS NOT NULL DROP TRIGGER [dbo].[TR_AspNetUserRoles_ReleaseAssignedVehicle];
IF OBJECT_ID(N'[dbo].[Vehicles]', N'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Vehicles]') AND name = N'UX_Vehicles_AssignedDriverId')
    DROP INDEX [UX_Vehicles_AssignedDriverId] ON [dbo].[Vehicles];

IF OBJECT_ID(N'[dbo].[Contracts]', N'U') IS NOT NULL AND COL_LENGTH('Contracts','AdminId') IS NULL
    ALTER TABLE Contracts ADD AdminId nvarchar(450) NULL;

-- Hợp đồng mới không còn bắt buộc CompanyProfile/Customer danh mục.
IF OBJECT_ID(N'[dbo].[Contracts]', N'U') IS NOT NULL
   AND COL_LENGTH('Contracts','CompanyProfileId') IS NOT NULL
   AND COLUMNPROPERTY(OBJECT_ID(N'[dbo].[Contracts]'), 'CompanyProfileId', 'AllowsNull') = 0
BEGIN
    DECLARE @fkCompany sysname;
    SELECT TOP 1 @fkCompany = fk.name
    FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
    WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[Contracts]')
      AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = N'CompanyProfileId';
    IF @fkCompany IS NOT NULL EXEC(N'ALTER TABLE [dbo].[Contracts] DROP CONSTRAINT [' + @fkCompany + N']');
    ALTER TABLE [dbo].[Contracts] ALTER COLUMN [CompanyProfileId] uniqueidentifier NULL;
END;

IF OBJECT_ID(N'[dbo].[Contracts]', N'U') IS NOT NULL
   AND COL_LENGTH('Contracts','CustomerId') IS NOT NULL
   AND COLUMNPROPERTY(OBJECT_ID(N'[dbo].[Contracts]'), 'CustomerId', 'AllowsNull') = 0
BEGIN
    DECLARE @fkCustomer sysname;
    SELECT TOP 1 @fkCustomer = fk.name
    FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
    WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[Contracts]')
      AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = N'CustomerId';
    IF @fkCustomer IS NOT NULL EXEC(N'ALTER TABLE [dbo].[Contracts] DROP CONSTRAINT [' + @fkCustomer + N']');
    ALTER TABLE [dbo].[Contracts] ALTER COLUMN [CustomerId] uniqueidentifier NULL;
END;

-- Chuyển thông tin CompanyProfile hiện hữu sang tài khoản Admin.
IF OBJECT_ID(N'[dbo].[CompanyProfiles]', N'U') IS NOT NULL
BEGIN
    UPDATE adminUser
       SET adminUser.CompanyName = COALESCE(NULLIF(adminUser.CompanyName, N''), company.CompanyName),
           adminUser.CompanyBranchName = COALESCE(NULLIF(adminUser.CompanyBranchName, N''), company.BranchName),
           adminUser.CompanyTaxCode = COALESCE(NULLIF(adminUser.CompanyTaxCode, N''), company.TaxCode),
           adminUser.CompanyBusinessLicenseNumber = COALESCE(NULLIF(adminUser.CompanyBusinessLicenseNumber, N''), company.BusinessLicenseNumber),
           adminUser.CompanyAddress = COALESCE(NULLIF(adminUser.CompanyAddress, N''), company.Address),
           adminUser.CompanyPhoneNumber = COALESCE(NULLIF(adminUser.CompanyPhoneNumber, N''), company.PhoneNumber),
           adminUser.CompanyEmail = COALESCE(NULLIF(adminUser.CompanyEmail, N''), company.Email),
           adminUser.CompanyRepresentativeName = COALESCE(NULLIF(adminUser.CompanyRepresentativeName, N''), company.RepresentativeName),
           adminUser.CompanyRepresentativePosition = COALESCE(NULLIF(adminUser.CompanyRepresentativePosition, N''), company.RepresentativePosition),
           adminUser.CompanyRepresentativeCitizenId = COALESCE(NULLIF(adminUser.CompanyRepresentativeCitizenId, N''), company.RepresentativeCitizenId),
           adminUser.CompanyRepresentativeCitizenIdIssuedDate = COALESCE(adminUser.CompanyRepresentativeCitizenIdIssuedDate, company.RepresentativeCitizenIdIssuedDate),
           adminUser.CompanyRepresentativeCitizenIdIssuedPlace = COALESCE(NULLIF(adminUser.CompanyRepresentativeCitizenIdIssuedPlace, N''), company.RepresentativeCitizenIdIssuedPlace),
           adminUser.CompanySignatureFileUrl = COALESCE(NULLIF(adminUser.CompanySignatureFileUrl, N''), company.RepresentativeSignatureFileUrl),
           adminUser.CompanySignatureHash = COALESCE(NULLIF(adminUser.CompanySignatureHash, N''), company.RepresentativeSignatureHash),
           adminUser.CompanySignedAt = COALESCE(adminUser.CompanySignedAt, company.RepresentativeSignedAt)
    FROM AspNetUsers adminUser
    INNER JOIN CompanyProfiles company ON company.Id = adminUser.CompanyProfileId
    WHERE EXISTS
    (
        SELECT 1 FROM AspNetUserRoles ur
        INNER JOIN AspNetRoles role ON role.Id = ur.RoleId
        WHERE ur.UserId = adminUser.Id AND role.Name = N'Admin'
    );

    -- Driver cũ được gắn với Admin cùng CompanyProfile. Nếu có nhiều Admin thì ưu tiên tài khoản hoạt động lâu nhất.
    UPDATE driver
       SET driver.AdminId = selectedAdmin.Id
    FROM AspNetUsers driver
    CROSS APPLY
    (
        SELECT TOP 1 adminUser.Id
        FROM AspNetUsers adminUser
        WHERE adminUser.CompanyProfileId = driver.CompanyProfileId
          AND adminUser.IsDeleted = 0
          AND EXISTS
          (
              SELECT 1 FROM AspNetUserRoles ur
              INNER JOIN AspNetRoles role ON role.Id = ur.RoleId
              WHERE ur.UserId = adminUser.Id AND role.Name = N'Admin'
          )
        ORDER BY adminUser.IsActive DESC, adminUser.CreatedAt, adminUser.Id
    ) selectedAdmin
    WHERE driver.AdminId IS NULL
      AND driver.CompanyProfileId IS NOT NULL
      AND EXISTS
      (
          SELECT 1 FROM AspNetUserRoles ur
          INNER JOIN AspNetRoles role ON role.Id = ur.RoleId
          WHERE ur.UserId = driver.Id AND role.Name = N'Driver'
      );

    IF OBJECT_ID(N'[dbo].[Vehicles]', N'U') IS NOT NULL
    BEGIN
        UPDATE vehicle
           SET vehicle.AdminId = selectedAdmin.Id
        FROM Vehicles vehicle
        CROSS APPLY
        (
            SELECT TOP 1 adminUser.Id
            FROM AspNetUsers adminUser
            WHERE adminUser.CompanyProfileId = vehicle.CompanyProfileId
              AND adminUser.IsDeleted = 0
              AND EXISTS
              (
                  SELECT 1 FROM AspNetUserRoles ur
                  INNER JOIN AspNetRoles role ON role.Id = ur.RoleId
                  WHERE ur.UserId = adminUser.Id AND role.Name = N'Admin'
              )
            ORDER BY adminUser.IsActive DESC, adminUser.CreatedAt, adminUser.Id
        ) selectedAdmin
        WHERE vehicle.AdminId IS NULL;
    END
END;

IF OBJECT_ID(N'[dbo].[Contracts]', N'U') IS NOT NULL
BEGIN
    UPDATE contract
       SET contract.AdminId = COALESCE(driver.AdminId, selectedAdmin.Id)
    FROM Contracts contract
    LEFT JOIN AspNetUsers driver ON driver.Id = contract.DriverId
    OUTER APPLY
    (
        SELECT TOP 1 adminUser.Id
        FROM AspNetUsers adminUser
        WHERE adminUser.CompanyProfileId = contract.CompanyProfileId
          AND adminUser.IsDeleted = 0
          AND EXISTS
          (
              SELECT 1 FROM AspNetUserRoles ur
              INNER JOIN AspNetRoles role ON role.Id = ur.RoleId
              WHERE ur.UserId = adminUser.Id AND role.Name = N'Admin'
          )
        ORDER BY adminUser.IsActive DESC, adminUser.CreatedAt, adminUser.Id
    ) selectedAdmin
    WHERE contract.AdminId IS NULL;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Contracts]') AND name = N'UX_Contracts_ContractNumber')
        DROP INDEX [UX_Contracts_ContractNumber] ON [dbo].[Contracts];

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Contracts]') AND name = N'UX_Contracts_Driver_ContractNumber')
        CREATE UNIQUE INDEX [UX_Contracts_Driver_ContractNumber] ON [dbo].[Contracts]([DriverId], [ContractNumber]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Contracts]') AND name = N'IX_Contracts_Admin_CreatedAt')
        CREATE INDEX [IX_Contracts_Admin_CreatedAt] ON [dbo].[Contracts]([AdminId], [CreatedAt]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]') AND name = N'IX_AspNetUsers_AdminId')
    CREATE INDEX [IX_AspNetUsers_AdminId] ON [dbo].[AspNetUsers]([AdminId]);
IF OBJECT_ID(N'[dbo].[Vehicles]', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Vehicles]') AND name = N'IX_Vehicles_AdminId')
    CREATE INDEX [IX_Vehicles_AdminId] ON [dbo].[Vehicles]([AdminId]);

-- Tạo lại FK cũ dưới dạng nullable và bổ sung FK AdminId sau khi đã backfill.
IF OBJECT_ID(N'[dbo].[Contracts]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[CompanyProfiles]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'[dbo].[Contracts]') AND name = N'FK_Contracts_CompanyProfiles_CompanyProfileId')
    ALTER TABLE [dbo].[Contracts] WITH NOCHECK ADD CONSTRAINT [FK_Contracts_CompanyProfiles_CompanyProfileId] FOREIGN KEY([CompanyProfileId]) REFERENCES [dbo].[CompanyProfiles]([Id]);
IF OBJECT_ID(N'[dbo].[Contracts]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[Customers]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'[dbo].[Contracts]') AND name = N'FK_Contracts_Customers_CustomerId')
    ALTER TABLE [dbo].[Contracts] WITH NOCHECK ADD CONSTRAINT [FK_Contracts_Customers_CustomerId] FOREIGN KEY([CustomerId]) REFERENCES [dbo].[Customers]([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'[dbo].[AspNetUsers]') AND name = N'FK_AspNetUsers_AspNetUsers_AdminId')
    ALTER TABLE [dbo].[AspNetUsers] WITH NOCHECK ADD CONSTRAINT [FK_AspNetUsers_AspNetUsers_AdminId] FOREIGN KEY([AdminId]) REFERENCES [dbo].[AspNetUsers]([Id]);
IF OBJECT_ID(N'[dbo].[Vehicles]', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'[dbo].[Vehicles]') AND name = N'FK_Vehicles_AspNetUsers_AdminId')
    ALTER TABLE [dbo].[Vehicles] WITH NOCHECK ADD CONSTRAINT [FK_Vehicles_AspNetUsers_AdminId] FOREIGN KEY([AdminId]) REFERENCES [dbo].[AspNetUsers]([Id]);
IF OBJECT_ID(N'[dbo].[Contracts]', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'[dbo].[Contracts]') AND name = N'FK_Contracts_AspNetUsers_AdminId')
    ALTER TABLE [dbo].[Contracts] WITH NOCHECK ADD CONSTRAINT [FK_Contracts_AspNetUsers_AdminId] FOREIGN KEY([AdminId]) REFERENCES [dbo].[AspNetUsers]([Id]);
");
    }


    private static async Task EnsureSoftDeleteColumnsAsync(ApplicationDbContext db)
    {
        // Hỗ trợ database cũ được tạo bằng EnsureCreated: bổ sung cột soft delete
        // trước khi bất kỳ query ApplicationUser/CompanyProfile nào được thực hiện.
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('AspNetUsers','IsDeleted') IS NULL
    ALTER TABLE AspNetUsers ADD IsDeleted bit NOT NULL CONSTRAINT DF_AspNetUsers_IsDeleted DEFAULT 0;
IF COL_LENGTH('AspNetUsers','DeletedAt') IS NULL
    ALTER TABLE AspNetUsers ADD DeletedAt datetime2 NULL;
IF COL_LENGTH('AspNetUsers','DeletedBy') IS NULL
    ALTER TABLE AspNetUsers ADD DeletedBy nvarchar(450) NULL;

IF COL_LENGTH('CompanyProfiles','IsDeleted') IS NULL
    ALTER TABLE CompanyProfiles ADD IsDeleted bit NOT NULL CONSTRAINT DF_CompanyProfiles_IsDeleted DEFAULT 0;
IF COL_LENGTH('CompanyProfiles','DeletedAt') IS NULL
    ALTER TABLE CompanyProfiles ADD DeletedAt datetime2 NULL;
IF COL_LENGTH('CompanyProfiles','DeletedBy') IS NULL
    ALTER TABLE CompanyProfiles ADD DeletedBy nvarchar(450) NULL;

IF OBJECT_ID(N'[dbo].[ContractAuditLogs]', N'U') IS NOT NULL AND COL_LENGTH('ContractAuditLogs','IsDeleted') IS NULL
    ALTER TABLE ContractAuditLogs ADD IsDeleted bit NOT NULL CONSTRAINT DF_ContractAuditLogs_IsDeleted DEFAULT 0;
IF OBJECT_ID(N'[dbo].[ContractAuditLogs]', N'U') IS NOT NULL AND COL_LENGTH('ContractAuditLogs','DeletedAt') IS NULL
    ALTER TABLE ContractAuditLogs ADD DeletedAt datetime2 NULL;
IF OBJECT_ID(N'[dbo].[ContractAuditLogs]', N'U') IS NOT NULL AND COL_LENGTH('ContractAuditLogs','DeletedBy') IS NULL
    ALTER TABLE ContractAuditLogs ADD DeletedBy nvarchar(450) NULL;


IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]') AND name = N'IX_AspNetUsers_IsDeleted')
    CREATE INDEX IX_AspNetUsers_IsDeleted ON AspNetUsers(IsDeleted);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[CompanyProfiles]') AND name = N'IX_CompanyProfiles_IsDeleted')
    CREATE INDEX IX_CompanyProfiles_IsDeleted ON CompanyProfiles(IsDeleted);

IF OBJECT_ID(N'[dbo].[ContractAuditLogs]', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[ContractAuditLogs]') AND name = N'IX_ContractAuditLogs_IsDeleted')
    CREATE INDEX IX_ContractAuditLogs_IsDeleted ON ContractAuditLogs(IsDeleted);

-- Database cũ có thể còn ON DELETE CASCADE/SET NULL. Chuyển về NO ACTION
-- để một lệnh DELETE vật lý không thể kéo theo việc mất dữ liệu liên quan.
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AspNetUsers_CompanyProfiles_CompanyProfileId' AND delete_referential_action <> 0)
BEGIN
    ALTER TABLE [dbo].[AspNetUsers] DROP CONSTRAINT [FK_AspNetUsers_CompanyProfiles_CompanyProfileId];
    ALTER TABLE [dbo].[AspNetUsers] WITH CHECK ADD CONSTRAINT [FK_AspNetUsers_CompanyProfiles_CompanyProfileId]
        FOREIGN KEY ([CompanyProfileId]) REFERENCES [dbo].[CompanyProfiles]([Id]);
    ALTER TABLE [dbo].[AspNetUsers] CHECK CONSTRAINT [FK_AspNetUsers_CompanyProfiles_CompanyProfileId];
END;


IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Vehicles_AspNetUsers_AssignedDriverId' AND delete_referential_action <> 0)
BEGIN
    ALTER TABLE [dbo].[Vehicles] DROP CONSTRAINT [FK_Vehicles_AspNetUsers_AssignedDriverId];
    ALTER TABLE [dbo].[Vehicles] WITH CHECK ADD CONSTRAINT [FK_Vehicles_AspNetUsers_AssignedDriverId]
        FOREIGN KEY ([AssignedDriverId]) REFERENCES [dbo].[AspNetUsers]([Id]);
    ALTER TABLE [dbo].[Vehicles] CHECK CONSTRAINT [FK_Vehicles_AspNetUsers_AssignedDriverId];
END;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Vehicles_CompanyProfiles_CompanyProfileId' AND delete_referential_action <> 0)
BEGIN
    ALTER TABLE [dbo].[Vehicles] DROP CONSTRAINT [FK_Vehicles_CompanyProfiles_CompanyProfileId];
    ALTER TABLE [dbo].[Vehicles] WITH CHECK ADD CONSTRAINT [FK_Vehicles_CompanyProfiles_CompanyProfileId]
        FOREIGN KEY ([CompanyProfileId]) REFERENCES [dbo].[CompanyProfiles]([Id]);
    ALTER TABLE [dbo].[Vehicles] CHECK CONSTRAINT [FK_Vehicles_CompanyProfiles_CompanyProfileId];
END;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ContractPassengers_Contracts_ContractId' AND delete_referential_action <> 0)
BEGIN
    ALTER TABLE [dbo].[ContractPassengers] DROP CONSTRAINT [FK_ContractPassengers_Contracts_ContractId];
    ALTER TABLE [dbo].[ContractPassengers] WITH CHECK ADD CONSTRAINT [FK_ContractPassengers_Contracts_ContractId]
        FOREIGN KEY ([ContractId]) REFERENCES [dbo].[Contracts]([Id]);
    ALTER TABLE [dbo].[ContractPassengers] CHECK CONSTRAINT [FK_ContractPassengers_Contracts_ContractId];
END;

-- Cho phép một SortOrder mới sau khi hành khách cũ đã bị ẩn mềm.
IF EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'[dbo].[ContractPassengers]')
      AND name = N'UX_ContractPassengers_Contract_SortOrder'
      AND filter_definition IS NULL
)
BEGIN
    DROP INDEX UX_ContractPassengers_Contract_SortOrder ON ContractPassengers;
    CREATE UNIQUE INDEX UX_ContractPassengers_Contract_SortOrder
        ON ContractPassengers(ContractId, SortOrder)
        WHERE IsDeleted = 0;
END
");
    }

    private static async Task EnsureDriverRegistrationColumnsAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('AspNetUsers','RegistrationStatus') IS NULL ALTER TABLE AspNetUsers ADD RegistrationStatus nvarchar(20) NOT NULL CONSTRAINT DF_AspNetUsers_RegistrationStatus DEFAULT 'Approved';
IF COL_LENGTH('AspNetUsers','RegistrationRequestedAt') IS NULL ALTER TABLE AspNetUsers ADD RegistrationRequestedAt datetime2 NULL;
IF COL_LENGTH('AspNetUsers','RegistrationViewedAt') IS NULL ALTER TABLE AspNetUsers ADD RegistrationViewedAt datetime2 NULL;
IF COL_LENGTH('AspNetUsers','RegistrationViewedByUserId') IS NULL ALTER TABLE AspNetUsers ADD RegistrationViewedByUserId nvarchar(450) NULL;
IF COL_LENGTH('AspNetUsers','RegistrationReviewedAt') IS NULL ALTER TABLE AspNetUsers ADD RegistrationReviewedAt datetime2 NULL;
IF COL_LENGTH('AspNetUsers','RegistrationReviewedByUserId') IS NULL ALTER TABLE AspNetUsers ADD RegistrationReviewedByUserId nvarchar(450) NULL;
IF COL_LENGTH('AspNetUsers','RegistrationReviewNote') IS NULL ALTER TABLE AspNetUsers ADD RegistrationReviewNote nvarchar(1000) NULL;
");
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in new[] { "Admin", "Driver" })
        {
            if (await roleManager.RoleExistsAsync(role)) continue;
            Ensure(await roleManager.CreateAsync(new IdentityRole(role)), $"Không thể tạo quyền {role}");
        }
    }

    private static async Task SeedDefaultAdminAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        var admins = (await userManager.GetUsersInRoleAsync("Admin"))
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.CreatedAt)
            .ToList();

        ApplicationUser? admin = admins.FirstOrDefault();
        if (admin is null)
        {
            var userName = configuration["Seed:AdminUserName"]?.Trim();
            if (string.IsNullOrWhiteSpace(userName)) userName = "admin";

            admin = await userManager.FindByNameAsync(userName);
            if (admin is not null && admin.IsDeleted)
                throw new InvalidOperationException($"Tài khoản seed '{userName}' đã bị ẩn mềm.");

            var password = configuration["Seed:AdminPassword"];
            var environmentName = configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"];
            if (string.IsNullOrWhiteSpace(password)
                && string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
                password = "Admin@123456";

            if (admin is null)
            {
                if (string.IsNullOrWhiteSpace(password))
                    throw new InvalidOperationException(
                        "Database chưa có Admin. Hãy cấu hình Seed:AdminPassword hoặc biến môi trường Seed__AdminPassword.");

                admin = new ApplicationUser
                {
                    UserName = userName,
                    FullName = "Quản trị công ty",
                    EmployeeCode = "ADMIN",
                    IsActive = true,
                    MustChangePassword = false,
                    RegistrationStatus = "Approved",
                    CreatedAt = DateTime.UtcNow
                };
                Ensure(await userManager.CreateAsync(admin, password), "Không thể tạo tài khoản Admin");
            }

            if (!await userManager.IsInRoleAsync(admin, "Admin"))
                Ensure(await userManager.AddToRoleAsync(admin, "Admin"), "Không thể gán quyền Admin");
        }

        if (await userManager.IsInRoleAsync(admin, "Driver"))
            Ensure(await userManager.RemoveFromRoleAsync(admin, "Driver"), "Không thể tách quyền Driver khỏi Admin");
        if (await userManager.IsInRoleAsync(admin, "Owner"))
            Ensure(await userManager.RemoveFromRoleAsync(admin, "Owner"), "Không thể loại bỏ quyền Owner cũ");

        var legacyCompany = admin.CompanyProfileId.HasValue
            ? await db.CompanyProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == admin.CompanyProfileId.Value)
            : await db.CompanyProfiles.IgnoreQueryFilters().OrderByDescending(x => x.IsActive).ThenBy(x => x.CreatedAt).FirstOrDefaultAsync();

        if (legacyCompany is not null)
        {
            admin.CompanyProfileId ??= legacyCompany.Id;
            CopyLegacyCompanyToAdmin(admin, legacyCompany);
        }

        admin.AdminId = null;
        admin.IsActive = true;
        admin.RegistrationStatus = "Approved";
        admin.UpdatedAt = DateTime.UtcNow;
        Ensure(await userManager.UpdateAsync(admin), "Không thể cập nhật tài khoản Admin mặc định");
    }

    private static void CopyLegacyCompanyToAdmin(ApplicationUser admin, CompanyProfile company)
    {
        admin.CompanyName = FirstValue(admin.CompanyName, company.CompanyName);
        admin.CompanyBranchName = FirstValue(admin.CompanyBranchName, company.BranchName);
        admin.CompanyTaxCode = FirstValue(admin.CompanyTaxCode, company.TaxCode);
        admin.CompanyBusinessLicenseNumber = FirstValue(admin.CompanyBusinessLicenseNumber, company.BusinessLicenseNumber);
        admin.CompanyAddress = FirstValue(admin.CompanyAddress, company.Address);
        admin.CompanyPhoneNumber = FirstValue(admin.CompanyPhoneNumber, company.PhoneNumber);
        admin.CompanyEmail = FirstValue(admin.CompanyEmail, company.Email);
        admin.CompanyRepresentativeName = FirstValue(admin.CompanyRepresentativeName, company.RepresentativeName);
        admin.CompanyRepresentativePosition = FirstValue(admin.CompanyRepresentativePosition, company.RepresentativePosition);
        admin.CompanyRepresentativeCitizenId = FirstValue(admin.CompanyRepresentativeCitizenId, company.RepresentativeCitizenId);
        admin.CompanyRepresentativeCitizenIdIssuedDate ??= company.RepresentativeCitizenIdIssuedDate;
        admin.CompanyRepresentativeCitizenIdIssuedPlace = FirstValue(admin.CompanyRepresentativeCitizenIdIssuedPlace, company.RepresentativeCitizenIdIssuedPlace);
        admin.CompanySignatureFileUrl = FirstValue(admin.CompanySignatureFileUrl, company.RepresentativeSignatureFileUrl);
        admin.CompanySignatureHash = FirstValue(admin.CompanySignatureHash, company.RepresentativeSignatureHash);
        admin.CompanySignedAt ??= company.RepresentativeSignedAt;
    }

    private static string? FirstValue(string? current, string? fallback)
        => string.IsNullOrWhiteSpace(current) ? fallback : current;

    private static async Task BackfillAdminOwnershipAsync(ApplicationDbContext db)
    {
        var adminRoleId = await db.Roles.AsNoTracking()
            .Where(x => x.Name == "Admin")
            .Select(x => x.Id)
            .FirstOrDefaultAsync();
        var driverRoleId = await db.Roles.AsNoTracking()
            .Where(x => x.Name == "Driver")
            .Select(x => x.Id)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(adminRoleId)) return;

        var adminIds = await db.UserRoles.AsNoTracking()
            .Where(x => x.RoleId == adminRoleId)
            .Select(x => x.UserId)
            .ToListAsync();
        var admins = await db.Users.IgnoreQueryFilters()
            .Where(x => adminIds.Contains(x.Id) && !x.IsDeleted)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync();
        if (admins.Count == 0) return;

        if (!string.IsNullOrWhiteSpace(driverRoleId))
        {
            var driverIds = await db.UserRoles.AsNoTracking()
                .Where(x => x.RoleId == driverRoleId)
                .Select(x => x.UserId)
                .ToListAsync();
            var drivers = await db.Users.IgnoreQueryFilters()
                .Where(x => driverIds.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync();
            foreach (var driver in drivers.Where(x => string.IsNullOrWhiteSpace(x.AdminId)))
            {
                var admin = admins.FirstOrDefault(x => x.CompanyProfileId == driver.CompanyProfileId) ?? admins[0];
                driver.AdminId = admin.Id;
                driver.CompanyProfileId ??= admin.CompanyProfileId;
                driver.UpdatedAt = DateTime.UtcNow;
            }
        }

        var vehicles = await db.Vehicles.IgnoreQueryFilters()
            .Where(x => x.AdminId == null)
            .ToListAsync();
        foreach (var vehicle in vehicles)
        {
            var admin = admins.FirstOrDefault(x => x.CompanyProfileId == vehicle.CompanyProfileId) ?? admins[0];
            vehicle.AdminId = admin.Id;
            vehicle.CompanyProfileId ??= admin.CompanyProfileId;
            vehicle.UpdatedAt = DateTime.UtcNow;
        }

        var contracts = await db.Contracts.IgnoreQueryFilters()
            .Where(x => x.AdminId == null)
            .ToListAsync();
        foreach (var contract in contracts)
        {
            var driverAdminId = await db.Users.IgnoreQueryFilters()
                .Where(x => x.Id == contract.DriverId)
                .Select(x => x.AdminId)
                .FirstOrDefaultAsync();
            var admin = admins.FirstOrDefault(x => x.Id == driverAdminId)
                        ?? admins.FirstOrDefault(x => x.CompanyProfileId == contract.CompanyProfileId)
                        ?? admins[0];
            contract.AdminId = admin.Id;
            contract.UpdatedAt ??= DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    private static async Task RemoveLegacyOwnerRoleAsync(ApplicationDbContext db)
    {
        var ownerRole = await db.Roles.FirstOrDefaultAsync(x => x.Name == "Owner");
        if (ownerRole is null) return;

        var assignments = await db.UserRoles
            .Where(x => x.RoleId == ownerRole.Id)
            .ToListAsync();
        var claims = await db.RoleClaims
            .Where(x => x.RoleId == ownerRole.Id)
            .ToListAsync();

        db.UserRoles.RemoveRange(assignments);
        db.RoleClaims.RemoveRange(claims);
        db.Roles.Remove(ownerRole);
        await db.SaveChangesAsync();
    }

    private static async Task SeedContractTypesAsync(ApplicationDbContext db)
    {
        // Giữ lại đúng hai loại hợp đồng theo nghiệp vụ hiện tại.
        // PASSENGER đang sử dụng; CARGO được tạo sẵn nhưng tạm khóa để chưa thể lập hợp đồng.
        var passengerType = await db.ContractTypes.FirstOrDefaultAsync(x => x.Code == "PASSENGER");
        if (passengerType is null)
        {
            // Nâng cấp dữ liệu cũ: tái sử dụng loại DRIVER để các hợp đồng đã có không mất liên kết.
            passengerType = await db.ContractTypes.FirstOrDefaultAsync(x => x.Code == "DRIVER");
            if (passengerType is not null)
                passengerType.Code = "PASSENGER";
        }

        passengerType ??= new ContractType
        {
            Id = Guid.NewGuid(),
            Code = "PASSENGER",
            CreatedAt = DateTime.UtcNow
        };

        if (db.Entry(passengerType).State == EntityState.Detached)
            db.ContractTypes.Add(passengerType);

        passengerType.Name = "HỢP ĐỒNG VẬN CHUYỂN HÀNH KHÁCH";
        passengerType.Description = "Hợp đồng vận chuyển hành khách bằng xe ô tô.";
        passengerType.IsActive = true;
        passengerType.RequireCustomerSignature = true;
        passengerType.RequireDriverSignature = true;
        passengerType.RequireLocation = true;
        passengerType.UpdatedAt = DateTime.UtcNow;

        var cargoType = await db.ContractTypes.FirstOrDefaultAsync(x => x.Code == "CARGO");
        if (cargoType is null)
        {
            cargoType = new ContractType
            {
                Id = Guid.NewGuid(),
                Code = "CARGO",
                CreatedAt = DateTime.UtcNow
            };
            db.ContractTypes.Add(cargoType);
        }

        cargoType.Name = "HỢP ĐỒNG VẬN CHUYỂN HÀNG HÓA BẰNG XE Ô TÔ (Tạm chưa dùng)";
        cargoType.Description = "Loại hợp đồng đã khai báo sẵn nhưng tạm thời chưa cho phép sử dụng.";
        cargoType.IsActive = false;
        cargoType.RequireCustomerSignature = true;
        cargoType.RequireDriverSignature = true;
        cargoType.RequireLocation = true;
        cargoType.UpdatedAt = DateTime.UtcNow;

        var legacyTypes = await db.ContractTypes
            .Where(x => x.Id != passengerType.Id && (x.Code == "DRIVER" || x.Code == "LONG_DISTANCE"))
            .ToListAsync();
        foreach (var legacyType in legacyTypes)
        {
            legacyType.IsActive = false;
            legacyType.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        await EnsureContractTemplateAsync(
            db,
            passengerType,
            "Mẫu HỢP ĐỒNG VẬN CHUYỂN HÀNH KHÁCH",
            isActive: true);

        await EnsureContractTemplateAsync(
            db,
            cargoType,
            "Mẫu HỢP ĐỒNG VẬN CHUYỂN HÀNG HÓA BẰNG XE Ô TÔ",
            isActive: false);
    }

    private static async Task EnsureContractTemplateAsync(
        ApplicationDbContext db,
        ContractType type,
        string name,
        bool isActive)
    {
        var templates = await db.ContractTemplates
            .Where(x => x.ContractTypeId == type.Id)
            .OrderByDescending(x => x.Version)
            .ToListAsync();

        // Ưu tiên giữ nguyên mẫu đang hoạt động để không ghi đè nội dung mẫu hợp đồng đã cấu hình.
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
                IsActive = isActive,
                EffectiveFrom = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            db.ContractTemplates.Add(template);
            templates.Add(template);
        }
        else
        {
            template.Name = name;
            template.IsActive = isActive;
            template.UpdatedAt = DateTime.UtcNow;
        }

        foreach (var other in templates.Where(x => x.Id != template.Id))
        {
            other.IsActive = false;
            other.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    private static readonly Guid DefaultCompanyProfileId =
    Guid.Parse("05860000-0000-0000-0000-000000000001");

    private static async Task SeedCompanyProfileAsync(ApplicationDbContext db)
    {
        var existed = await db.CompanyProfiles
            .AnyAsync(x => x.TaxCode == "1801774247");

        if (existed)
            return;

        db.CompanyProfiles.Add(new CompanyProfile
        {
            Id = DefaultCompanyProfileId,

            CompanyName = "HỢP TÁC XÃ VẬN TẢI 586 - CẦN THƠ",
            BranchName = "CẦN THƠ",
            TaxCode = "1801774247",
            BusinessLicenseNumber = "92240166/GPKDVT",

            Address = "Khu dân cư lô số 11B - KĐT Nam Cần Thơ, Phường Cái Răng, Thành phố Cần Thơ",
            PhoneNumber = "0939656507",

            RepresentativeName = "Nguyễn Việt Kiều Anh",
            RepresentativeCitizenId = "092196007693",
            RepresentativeCitizenIdIssuedDate = new DateTime(2021, 8, 14),
            RepresentativeCitizenIdIssuedPlace = null,
            RepresentativePosition = "Người đại diện",

            RepresentativeSignatureFileUrl = null,
            RepresentativeSignatureHash = null,
            RepresentativeSignedAt = null,

            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedDemoDataAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        var company = await db.CompanyProfiles
            .FirstOrDefaultAsync(x => x.TaxCode == "1801774247" && x.IsActive && !x.IsDeleted);

        if (company is null)
            return;

        var admin = await (from user in db.Users
                           join userRole in db.UserRoles on user.Id equals userRole.UserId
                           join role in db.Roles on userRole.RoleId equals role.Id
                           where role.Name == "Admin" && user.IsActive && !user.IsDeleted
                           orderby user.CreatedAt
                           select user).FirstOrDefaultAsync();
        if (admin is null) return;

        var demoPassword = configuration["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(demoPassword))
            demoPassword = "Driver@123";

        var drivers = new List<ApplicationUser>();
        for (var index = 1; index <= 9; index++)
        {
            var userName = $"driverdemo{index:00}";
            var driver = await userManager.FindByNameAsync(userName);

            if (driver?.IsDeleted == true)
                continue;

            if (driver is null)
            {
                driver = new ApplicationUser
                {
                    UserName = userName,
                    PhoneNumber = $"09{index:00}586{index:000}",
                    FullName = $"Tài xế mẫu {index:00}",
                    EmployeeCode = $"DRV{index:000}",
                    CompanyProfileId = company.Id,
                    AdminId = admin.Id,
                    AreaCode = company.BranchName ?? "CẦN THƠ",
                    DriverLicenseNumber = $"GPLX-DEMO-{index:000}",
                    DriverLicenseClass = index % 2 == 0 ? "D" : "B2",
                    RegistrationStatus = "Approved",
                    IsActive = true,
                    MustChangePassword = false,
                    CreatedAt = DateTime.UtcNow
                };

                Ensure(
                    await userManager.CreateAsync(driver, demoPassword),
                    $"Không thể tạo tài xế mẫu {userName}");
            }
            else
            {
                driver.CompanyProfileId = company.Id;
                driver.AdminId = admin.Id;
                driver.IsActive = true;
                driver.RegistrationStatus = "Approved";
                driver.MustChangePassword = false;
                Ensure(
                    await userManager.UpdateAsync(driver),
                    $"Không thể cập nhật tài xế mẫu {userName}");
            }

            if (!await userManager.IsInRoleAsync(driver, "Driver"))
            {
                Ensure(
                    await userManager.AddToRoleAsync(driver, "Driver"),
                    $"Không thể gán quyền Driver cho {userName}");
            }

            drivers.Add(driver);
        }

        if (drivers.Count == 0)
            return;

        var vehicles = new List<Vehicle>();
        for (var index = 1; index <= 10; index++)
        {
            var plate = $"65A-{58600 + index}";
            var vehicle = await db.Vehicles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.PlateNumber == plate);

            if (vehicle?.IsDeleted == true)
                continue;

            if (vehicle is null)
            {
                vehicle = new Vehicle
                {
                    Id = Guid.NewGuid(),
                    PlateNumber = plate,
                    VehicleCode = $"XE-DEMO-{index:000}",
                    Brand = index % 2 == 0 ? "Toyota" : "Kia",
                    Model = index % 2 == 0 ? "Innova" : "Carnival",
                    VehicleType = "Xe hợp đồng",
                    SeatCount = index % 2 == 0 ? 7 : 8,
                    Color = index % 2 == 0 ? "Trắng" : "Bạc",
                    OwnerName = $"Chủ sở hữu mẫu {index:00}",
                    OwnerCitizenId = $"09220600{index:04}",
                    OwnerPhoneNumber = $"08{index:00}586{index:000}",
                    OwnerAddress = "Cần Thơ",
                    CompanyProfileId = company.Id,
                    AdminId = admin.Id,
                    AssignedDriverId = index <= drivers.Count ? drivers[index - 1].Id : null,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "DEMO-SEED"
                };
                db.Vehicles.Add(vehicle);
            }
            else
            {
                vehicle.CompanyProfileId = company.Id;
                vehicle.AdminId = admin.Id;
                vehicle.AssignedDriverId = index <= drivers.Count ? drivers[index - 1].Id : null;
                vehicle.IsActive = true;
                vehicle.UpdatedAt = DateTime.UtcNow;
                vehicle.UpdatedBy = "DEMO-SEED";
            }

            vehicles.Add(vehicle);
        }

        await db.SaveChangesAsync();

        var pairCount = Math.Min(drivers.Count, vehicles.Count);
        if (pairCount == 0)
            return;
        if (drivers.Count != pairCount)
            drivers = drivers.Take(pairCount).ToList();
        if (vehicles.Count != pairCount)
            vehicles = vehicles.Take(pairCount).ToList();

        var customers = new List<Customer>();
        for (var index = 1; index <= 5; index++)
        {
            var phone = $"07{index:00}586{index:000}";
            var creatorId = drivers[(index - 1) % drivers.Count].Id;
            var customer = await db.Customers
                .FirstOrDefaultAsync(x => x.PhoneNumber == phone);

            if (customer is null)
            {
                customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    Type = CustomerType.Individual,
                    FullName = $"Khách hàng mẫu {index:00}",
                    PhoneNumber = phone,
                    CitizenId = $"09230600{index:04}",
                    Address = $"Địa chỉ khách hàng mẫu {index:00}, Cần Thơ",
                    CreatedByDriverId = creatorId,
                    LastUsedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = creatorId
                };
                db.Customers.Add(customer);
            }

            customers.Add(customer);
        }

        await db.SaveChangesAsync();

        var type = await db.ContractTypes
            .FirstAsync(x => x.Code == "PASSENGER" && x.IsActive);
        var template = await db.ContractTemplates
            .FirstAsync(x => x.ContractTypeId == type.Id && x.IsActive);
        var createdBy = admin.Id;
        var createdByName = admin.FullName;
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 1, 0, 0, DateTimeKind.Utc);

        for (var driverIndex = 0; driverIndex < drivers.Count; driverIndex++)
        {
            var driver = drivers[driverIndex];
            var vehicle = vehicles[driverIndex];

            for (var contractIndex = 1; contractIndex <= 5; contractIndex++)
            {
                var contractNumber = $"DEMO-{monthStart:yyyyMM}-{driverIndex + 1:00}-{contractIndex:00}";
                var customer = customers[(driverIndex + contractIndex - 1) % customers.Count];
                var createdAt = monthStart.AddDays((driverIndex + contractIndex) % 14).AddHours(7 + contractIndex);
                var status = contractIndex <= 3
                    ? ContractStatus.Completed
                    : contractIndex == 4
                        ? ContractStatus.Draft
                        : ContractStatus.WaitingCustomerSignature;
                var startTime = createdAt.AddDays(1);
                var endTime = startTime.AddHours(3 + contractIndex);
                var passengerCount = 1 + ((driverIndex + contractIndex) % 4);

                var existingContract = await db.Contracts
                    .IgnoreQueryFilters()
                    .Include(x => x.Passengers)
                    .FirstOrDefaultAsync(x => x.ContractNumber == contractNumber);

                if (existingContract is not null)
                {
                    if (existingContract.IsDeleted)
                        continue;

                    existingContract.BusinessType = ContractBusinessType.Passenger;
                    existingContract.ContractTypeId = type.Id;
                    existingContract.ContractTemplateId = template.Id;
                    existingContract.ActualPassengerCount = passengerCount;
                    existingContract.UpdatedAt = DateTime.UtcNow;
                    existingContract.UpdatedBy = "DEMO-SEED";

                    SyncDemoPassengers(
                        db,
                        existingContract,
                        passengerCount,
                        driverIndex,
                        contractIndex,
                        createdAt,
                        createdBy);
                    continue;
                }

                var contract = new Contract
                {
                    Id = Guid.NewGuid(),
                    ContractNumber = contractNumber,
                    BusinessType = ContractBusinessType.Passenger,
                    ContractTypeId = type.Id,
                    ContractTemplateId = template.Id,
                    CompanyProfileId = company.Id,
                    AdminId = admin.Id,
                    DriverId = driver.Id,
                    CustomerId = customer.Id,
                    VehicleId = vehicle.Id,
                    Status = status,
                    AreaCode = driver.AreaCode ?? "CẦN THƠ",
                    ActualPassengerCount = passengerCount,
                    PickupLocation = "Bến xe Trung tâm Cần Thơ",
                    DropoffLocation = $"Điểm đến mẫu {contractIndex:00}",
                    StartTime = startTime,
                    EndTime = endTime,
                    RouteDescription = $"Lộ trình mẫu của {driver.FullName}, chuyến {contractIndex:00}",
                    TotalKilometers = 35 + driverIndex * 4 + contractIndex * 7,
                    ContractValue = 650000 + driverIndex * 50000 + contractIndex * 100000,
                    PaymentMethod = contractIndex % 2 == 0 ? "Chuyển khoản" : "Tiền mặt",
                    PaymentTime = "Thanh toán sau khi kết thúc chuyến đi",
                    Note = "Dữ liệu mẫu được tạo tự động.",
                    CompanyNameSnapshot = company.CompanyName,
                    CompanyTaxCodeSnapshot = company.TaxCode,
                    CompanyAddressSnapshot = company.Address,
                    CompanyRepresentativeSnapshot = company.RepresentativeName,
                    CompanyRepresentativePositionSnapshot = company.RepresentativePosition,
                    DriverNameSnapshot = driver.FullName,
                    DriverLicenseNumberSnapshot = driver.DriverLicenseNumber,
                    DriverLicenseClassSnapshot = driver.DriverLicenseClass,
                    CustomerNameSnapshot = customer.FullName,
                    CustomerPhoneSnapshot = customer.PhoneNumber,
                    CustomerCitizenIdSnapshot = customer.CitizenId,
                    CustomerAddressSnapshot = customer.Address,
                    VehiclePlateSnapshot = vehicle.PlateNumber,
                    VehicleBrandSnapshot = vehicle.Brand,
                    VehicleOwnerNameSnapshot = vehicle.OwnerName,
                    VehicleOwnerCitizenIdSnapshot = vehicle.OwnerCitizenId,
                    ContractContentSnapshot = template.HtmlContent,
                    ContractDataJson = "{}",
                    CompletedAt = status == ContractStatus.Completed ? endTime : null,
                    CreatedAt = createdAt,
                    CreatedBy = createdBy
                };

                SyncDemoPassengers(
                    db,
                    contract,
                    passengerCount,
                    driverIndex,
                    contractIndex,
                    createdAt,
                    createdBy);

                contract.AuditLogs.Add(new ContractAuditLog
                {
                    ContractId = contract.Id,
                    Action = "AssignedToDriver",
                    UserId = createdBy,
                    UserName = createdByName,
                    NewDataJson = $"{{\"driverId\":\"{driver.Id}\",\"vehicleId\":\"{vehicle.Id}\"}}",
                    CreatedAt = createdAt
                });

                db.Contracts.Add(contract);
            }
        }

        await db.SaveChangesAsync();
    }


    private static async Task BackfillContractSnapshotsAsync(ApplicationDbContext db)
    {
        var legacyContracts = await db.Contracts
            .Include(x => x.CompanyProfile)
            .Include(x => x.Driver)
            .Include(x => x.Customer)
            .Include(x => x.Vehicle)
            .Where(x => x.ContractDataJson == null || x.ContractDataJson == "{}" || x.ContractDataJson == string.Empty)
            .ToListAsync();

        if (legacyContracts.Count == 0)
            return;

        foreach (var contract in legacyContracts)
            contract.ContractDataJson = ContractSnapshotData.CaptureLegacy(contract).ToJson();

        await db.SaveChangesAsync();
    }

    private static void SyncDemoPassengers(
        ApplicationDbContext db,
        Contract contract,
        int passengerCount,
        int driverIndex,
        int contractIndex,
        DateTime createdAt,
        string createdBy)
    {
        var extras = contract.Passengers
            .Where(x => x.SortOrder > passengerCount)
            .ToList();
        if (extras.Count > 0)
        {
            db.ContractPassengers.RemoveRange(extras);
            foreach (var extra in extras)
                contract.Passengers.Remove(extra);
        }

        for (var passengerIndex = 1; passengerIndex <= passengerCount; passengerIndex++)
        {
            var passenger = contract.Passengers
                .FirstOrDefault(x => x.SortOrder == passengerIndex);

            if (passenger is null)
            {
                passenger = new ContractPassenger
                {
                    ContractId = contract.Id,
                    SortOrder = passengerIndex,
                    CreatedAt = createdAt,
                    CreatedBy = createdBy
                };
                contract.Passengers.Add(passenger);
            }

            passenger.FullName = $"Hành khách mẫu {driverIndex + 1:00}-{contractIndex:00}-{passengerIndex:00}";
            passenger.BirthYear = 1985 + ((driverIndex + contractIndex + passengerIndex) % 20);
            passenger.Note = passengerIndex == 1 ? "Người đại diện nhóm khách" : null;
            passenger.UpdatedAt = DateTime.UtcNow;
            passenger.UpdatedBy = "DEMO-SEED";
        }
    }

    private static void Ensure(IdentityResult result, string message)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"{message}: {string.Join("; ", result.Errors.Select(x => x.Description))}");
    }
}
