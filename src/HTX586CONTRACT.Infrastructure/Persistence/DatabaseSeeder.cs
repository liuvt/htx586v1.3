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
        await EnsureDriverRegistrationColumnsAsync(db);
        await EnsureDriverNotificationTableAsync(db);
        await EnsureSoftDeleteColumnsAsync(db);
        await EnsureVehicleOwnerSchemaAsync(db);

        await SeedRolesAsync(roleManager);
        await MigrateLegacyDriverRoleAsync(db);
        await SeedOwnerAsync(userManager, configuration);

        // Mặc đinh Owner tạo CompanyProfile riêng và gán cho Admin/Driver/Drive.
        await SeedCompanyProfileAsync(db);
        
        // Owner tạo CompanyProfile riêng và gán cho Admin/Driver/Drive.
        await SeedContractTypesAsync(db);

        if (configuration.GetValue<bool>("Seed:DemoDataEnabled"))
            await SeedDemoDataAsync(db, userManager, configuration);

        await BackfillContractSnapshotsAsync(db);
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

IF OBJECT_ID(N'[dbo].[DriverNotifications]', N'U') IS NOT NULL AND COL_LENGTH('DriverNotifications','IsDeleted') IS NULL
    ALTER TABLE DriverNotifications ADD IsDeleted bit NOT NULL CONSTRAINT DF_DriverNotifications_IsDeleted DEFAULT 0;
IF OBJECT_ID(N'[dbo].[DriverNotifications]', N'U') IS NOT NULL AND COL_LENGTH('DriverNotifications','DeletedAt') IS NULL
    ALTER TABLE DriverNotifications ADD DeletedAt datetime2 NULL;
IF OBJECT_ID(N'[dbo].[DriverNotifications]', N'U') IS NOT NULL AND COL_LENGTH('DriverNotifications','DeletedBy') IS NULL
    ALTER TABLE DriverNotifications ADD DeletedBy nvarchar(450) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]') AND name = N'IX_AspNetUsers_IsDeleted')
    CREATE INDEX IX_AspNetUsers_IsDeleted ON AspNetUsers(IsDeleted);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[CompanyProfiles]') AND name = N'IX_CompanyProfiles_IsDeleted')
    CREATE INDEX IX_CompanyProfiles_IsDeleted ON CompanyProfiles(IsDeleted);

IF OBJECT_ID(N'[dbo].[ContractAuditLogs]', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[ContractAuditLogs]') AND name = N'IX_ContractAuditLogs_IsDeleted')
    CREATE INDEX IX_ContractAuditLogs_IsDeleted ON ContractAuditLogs(IsDeleted);
IF OBJECT_ID(N'[dbo].[DriverNotifications]', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[DriverNotifications]') AND name = N'IX_DriverNotifications_IsDeleted')
    CREATE INDEX IX_DriverNotifications_IsDeleted ON DriverNotifications(IsDeleted);

-- Database cũ có thể còn ON DELETE CASCADE/SET NULL. Chuyển về NO ACTION
-- để một lệnh DELETE vật lý không thể kéo theo việc mất dữ liệu liên quan.
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AspNetUsers_CompanyProfiles_CompanyProfileId' AND delete_referential_action <> 0)
BEGIN
    ALTER TABLE [dbo].[AspNetUsers] DROP CONSTRAINT [FK_AspNetUsers_CompanyProfiles_CompanyProfileId];
    ALTER TABLE [dbo].[AspNetUsers] WITH CHECK ADD CONSTRAINT [FK_AspNetUsers_CompanyProfiles_CompanyProfileId]
        FOREIGN KEY ([CompanyProfileId]) REFERENCES [dbo].[CompanyProfiles]([Id]);
    ALTER TABLE [dbo].[AspNetUsers] CHECK CONSTRAINT [FK_AspNetUsers_CompanyProfiles_CompanyProfileId];
END;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DriverNotifications_AspNetUsers_DriverId' AND delete_referential_action <> 0)
BEGIN
    ALTER TABLE [dbo].[DriverNotifications] DROP CONSTRAINT [FK_DriverNotifications_AspNetUsers_DriverId];
    ALTER TABLE [dbo].[DriverNotifications] WITH CHECK ADD CONSTRAINT [FK_DriverNotifications_AspNetUsers_DriverId]
        FOREIGN KEY ([DriverId]) REFERENCES [dbo].[AspNetUsers]([Id]);
    ALTER TABLE [dbo].[DriverNotifications] CHECK CONSTRAINT [FK_DriverNotifications_AspNetUsers_DriverId];
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

    private static async Task EnsureDriverNotificationTableAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[dbo].[DriverNotifications]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DriverNotifications]
    (
        [Id] uniqueidentifier NOT NULL,
        [DriverId] nvarchar(450) NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        [LinkUrl] nvarchar(500) NULL,
        [RelatedContractId] uniqueidentifier NULL,
        [RelatedVehicleId] uniqueidentifier NULL,
        [IsRead] bit NOT NULL CONSTRAINT [DF_DriverNotifications_IsRead] DEFAULT 0,
        [CreatedAt] datetime2 NOT NULL,
        [ReadAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_DriverNotifications_IsDeleted] DEFAULT 0,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_DriverNotifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DriverNotifications_AspNetUsers_DriverId]
            FOREIGN KEY ([DriverId]) REFERENCES [dbo].[AspNetUsers]([Id])
    );

    CREATE INDEX [IX_DriverNotifications_Driver_Read_CreatedAt]
        ON [dbo].[DriverNotifications] ([DriverId], [IsRead], [CreatedAt] DESC);
    CREATE INDEX [IX_DriverNotifications_IsDeleted]
        ON [dbo].[DriverNotifications] ([IsDeleted]);
END
");
    }

    private static async Task EnsureVehicleOwnerSchemaAsync(ApplicationDbContext db)
    {
        // Nâng cấp an toàn database cũ sang mô hình VehicleOwner:
        // - một tài khoản có nhiều xe;
        // - tài khoản không gán trực tiếp Công ty/Văn phòng;
        // - chữ ký tài xế được lưu theo từng xe;
        // - hợp đồng có trạng thái nhận/khóa và thông tin người phát.
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('AspNetUsers','CreatedByUserId') IS NULL ALTER TABLE AspNetUsers ADD CreatedByUserId nvarchar(450) NULL;
IF COL_LENGTH('AspNetUsers','UpdatedByUserId') IS NULL ALTER TABLE AspNetUsers ADD UpdatedByUserId nvarchar(450) NULL;

IF COL_LENGTH('CompanyProfiles','CreatedByUserId') IS NULL ALTER TABLE CompanyProfiles ADD CreatedByUserId nvarchar(450) NULL;
IF COL_LENGTH('CompanyProfiles','UpdatedByUserId') IS NULL ALTER TABLE CompanyProfiles ADD UpdatedByUserId nvarchar(450) NULL;

IF OBJECT_ID(N'[dbo].[Vehicles]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('Vehicles','AccountDriverSignatureFileUrl') IS NULL ALTER TABLE Vehicles ADD AccountDriverSignatureFileUrl nvarchar(500) NULL;
    IF COL_LENGTH('Vehicles','AccountDriverSignatureHash') IS NULL ALTER TABLE Vehicles ADD AccountDriverSignatureHash nvarchar(128) NULL;
    IF COL_LENGTH('Vehicles','AccountDriverSignedAt') IS NULL ALTER TABLE Vehicles ADD AccountDriverSignedAt datetime2 NULL;
    IF COL_LENGTH('Vehicles','AccountDriverSignedByUserId') IS NULL ALTER TABLE Vehicles ADD AccountDriverSignedByUserId nvarchar(450) NULL;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Vehicles]') AND name = N'UX_Vehicles_AssignedDriverId')
        DROP INDEX [UX_Vehicles_AssignedDriverId] ON [dbo].[Vehicles];
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Vehicles]') AND name = N'IX_Vehicles_AssignedDriverId')
        DROP INDEX [IX_Vehicles_AssignedDriverId] ON [dbo].[Vehicles];
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Vehicles]') AND name = N'IX_Vehicles_AssignedVehicleOwnerId')
        CREATE INDEX [IX_Vehicles_AssignedVehicleOwnerId] ON [dbo].[Vehicles]([AssignedDriverId]);
END;

IF OBJECT_ID(N'[dbo].[Contracts]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('Contracts','IsSelfCreated') IS NULL ALTER TABLE Contracts ADD IsSelfCreated bit NOT NULL CONSTRAINT DF_Contracts_IsSelfCreated DEFAULT 0;
    IF COL_LENGTH('Contracts','AssignedByUserId') IS NULL ALTER TABLE Contracts ADD AssignedByUserId nvarchar(450) NULL;
    IF COL_LENGTH('Contracts','AssignedByNameSnapshot') IS NULL ALTER TABLE Contracts ADD AssignedByNameSnapshot nvarchar(200) NULL;
    IF COL_LENGTH('Contracts','AssignedAt') IS NULL ALTER TABLE Contracts ADD AssignedAt datetime2 NULL;
    IF COL_LENGTH('Contracts','ReceivedAt') IS NULL ALTER TABLE Contracts ADD ReceivedAt datetime2 NULL;
    IF COL_LENGTH('Contracts','LockedAt') IS NULL ALTER TABLE Contracts ADD LockedAt datetime2 NULL;
    IF COL_LENGTH('Contracts','CustomerTravelsWithGroup') IS NULL ALTER TABLE Contracts ADD CustomerTravelsWithGroup bit NOT NULL CONSTRAINT DF_Contracts_CustomerTravelsWithGroup DEFAULT 0;
    IF COL_LENGTH('Contracts','OperatingDriverName') IS NULL ALTER TABLE Contracts ADD OperatingDriverName nvarchar(200) NULL;
    IF COL_LENGTH('Contracts','OperatingDriverPhoneNumber') IS NULL ALTER TABLE Contracts ADD OperatingDriverPhoneNumber nvarchar(20) NULL;
    IF COL_LENGTH('Contracts','OperatingDriverLicenseNumber') IS NULL ALTER TABLE Contracts ADD OperatingDriverLicenseNumber nvarchar(50) NULL;
    IF COL_LENGTH('Contracts','OperatingDriverLicenseClass') IS NULL ALTER TABLE Contracts ADD OperatingDriverLicenseClass nvarchar(20) NULL;
END;

-- Xóa trigger của mô hình cũ vì trigger này bắt buộc tài khoản phải cùng CompanyProfile
-- và giới hạn một tài khoản chỉ được nhận một xe.
IF OBJECT_ID(N'[dbo].[TR_AspNetUsers_ReleaseAssignedVehicle]', N'TR') IS NOT NULL DROP TRIGGER [dbo].[TR_AspNetUsers_ReleaseAssignedVehicle];
IF OBJECT_ID(N'[dbo].[TR_Vehicles_ValidateAssignedDriver]', N'TR') IS NOT NULL DROP TRIGGER [dbo].[TR_Vehicles_ValidateAssignedDriver];
IF OBJECT_ID(N'[dbo].[TR_AspNetUserRoles_ReleaseAssignedVehicle]', N'TR') IS NOT NULL DROP TRIGGER [dbo].[TR_AspNetUserRoles_ReleaseAssignedVehicle];
");
    }

    private static async Task MigrateLegacyDriverRoleAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(@"
DECLARE @OldRoleId nvarchar(450) = (SELECT TOP 1 Id FROM AspNetRoles WHERE NormalizedName = N'DRIVER');
DECLARE @NewRoleId nvarchar(450) = (SELECT TOP 1 Id FROM AspNetRoles WHERE NormalizedName = N'VEHICLEOWNER');

IF @OldRoleId IS NOT NULL AND @NewRoleId IS NOT NULL
BEGIN
    INSERT INTO AspNetUserRoles(UserId, RoleId)
    SELECT oldMap.UserId, @NewRoleId
    FROM AspNetUserRoles oldMap
    WHERE oldMap.RoleId = @OldRoleId
      AND NOT EXISTS (SELECT 1 FROM AspNetUserRoles currentMap WHERE currentMap.UserId = oldMap.UserId AND currentMap.RoleId = @NewRoleId);

    DELETE FROM AspNetUserRoles WHERE RoleId = @OldRoleId;
    DELETE FROM AspNetRoleClaims WHERE RoleId = @OldRoleId;
    DELETE FROM AspNetRoles WHERE Id = @OldRoleId;
END;

-- VehicleOwner không thuộc trực tiếp một Công ty/Văn phòng.
UPDATE users
SET users.CompanyProfileId = NULL,
    users.UpdatedAt = SYSUTCDATETIME(),
    users.UpdatedByUserId = N'SCHEMA_VEHICLEOWNER_MIGRATION'
FROM AspNetUsers users
INNER JOIN AspNetUserRoles map ON map.UserId = users.Id
INNER JOIN AspNetRoles role ON role.Id = map.RoleId
WHERE role.NormalizedName = N'VEHICLEOWNER' AND users.CompanyProfileId IS NOT NULL;
");
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in new[] { "Owner", "Admin", "VehicleOwner" })
        {
            if (await roleManager.RoleExistsAsync(role)) continue;
            Ensure(await roleManager.CreateAsync(new IdentityRole(role)), $"Không thể tạo quyền {role}");
        }
    }

    private static async Task SeedOwnerAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        var userName = configuration["Seed:OwnerUserName"]?.Trim();
        if (string.IsNullOrWhiteSpace(userName)) userName = "owner";

        var password = configuration["Seed:OwnerPassword"];
        if (string.IsNullOrWhiteSpace(password)) password = "Htx@586";

        var owner = await userManager.FindByNameAsync(userName);
        if (owner is null)
        {
            owner = new ApplicationUser
            {
                UserName = userName,
                FullName = configuration["Seed:OwnerFullName"]?.Trim() ?? "Owner HTX586",
                EmployeeCode = configuration["Seed:OwnerEmployeeCode"]?.Trim() ?? "OWNER001",
                PhoneNumber = configuration["Seed:OwnerPhoneNumber"]?.Trim() ?? "0900000586",
                Email = configuration["Seed:OwnerEmail"]?.Trim(),
                RegistrationStatus = "Approved",
                IsActive = true,
                MustChangePassword = false,
                CreatedByUserId = "SYSTEM_SEED",
                CreatedAt = DateTime.UtcNow
            };

            Ensure(await userManager.CreateAsync(owner, password), "Không thể tạo tài khoản Owner seed");
        }
        else if (owner.IsDeleted)
        {
            throw new InvalidOperationException($"Tài khoản Owner seed '{userName}' đã bị xóa mềm. Hãy đổi Seed:OwnerUserName hoặc khôi phục tài khoản trong database.");
        }

        await EnsureOwnerOnlyAsync(userManager, owner);
    }

    private static async Task EnsureOwnerOnlyAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user)
    {
        if (user.IsDeleted)
            throw new InvalidOperationException("Không thể tự động kích hoạt lại tài khoản Owner đã bị ẩn mềm.");

        if (!await userManager.IsInRoleAsync(user, "Owner"))
            Ensure(await userManager.AddToRoleAsync(user, "Owner"), "Không thể gán quyền Owner");

        if (await userManager.IsInRoleAsync(user, "Admin"))
            Ensure(await userManager.RemoveFromRoleAsync(user, "Admin"), "Không thể tách quyền Admin khỏi Owner");

        if (await userManager.IsInRoleAsync(user, "VehicleOwner"))
            Ensure(await userManager.RemoveFromRoleAsync(user, "VehicleOwner"), "Không thể tách quyền VehicleOwner khỏi Owner");

        user.CompanyProfileId = null;
        user.UpdatedAt = DateTime.UtcNow;

        Ensure(await userManager.UpdateAsync(user), "Không thể cập nhật tài khoản Owner");
    }

    private static async Task SeedContractTypesAsync(ApplicationDbContext db)
    {
        // Giữ lại đúng hai loại hợp đồng theo nghiệp vụ hiện tại.
        // PASSENGER và CARGO đều được kích hoạt theo nghiệp vụ mới.
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

        cargoType.Name = "HỢP ĐỒNG VẬN CHUYỂN HÀNG HÓA BẰNG XE Ô TÔ";
        cargoType.Description = "Hợp đồng vận chuyển hàng hóa bằng xe ô tô.";
        cargoType.IsActive = true;
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
            isActive: true);
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
            CreatedByUserId = "SYSTEM_SEED",
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

        var demoPassword = configuration["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(demoPassword))
            demoPassword = "Htx@586";

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
                    FullName = $"Chủ xe mẫu {index:00}",
                    EmployeeCode = $"DRV{index:000}",
                    CompanyProfileId = null,
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
                driver.CompanyProfileId = null;
                driver.IsActive = true;
                driver.RegistrationStatus = "Approved";
                driver.MustChangePassword = false;
                Ensure(
                    await userManager.UpdateAsync(driver),
                    $"Không thể cập nhật tài xế mẫu {userName}");
            }

            if (!await userManager.IsInRoleAsync(driver, "VehicleOwner"))
            {
                Ensure(
                    await userManager.AddToRoleAsync(driver, "VehicleOwner"),
                    $"Không thể gán quyền VehicleOwner cho {userName}");
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
                    CompanyProfileId = null,
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
        var owner = (await userManager.GetUsersInRoleAsync("Owner")).FirstOrDefault();
        var createdBy = owner?.Id ?? drivers[0].Id;
        var createdByName = owner?.FullName ?? drivers[0].FullName;
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
