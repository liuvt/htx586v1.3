using ClosedXML.Excel;
using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Domain.Notifications;
using HTX586CONTRACT.Domain.Offices;
using HTX586CONTRACT.Domain.Vehicles;
using HTX586CONTRACT.Infrastructure.Identity;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Web.Services;

public sealed class VehicleExcelImportService(
    IDbContextFactory<ApplicationDbContext> factory,
    IOfficeAccessService officeAccessService,
    SafeUserManager userManager)
{
    public const string WorksheetName = "IMPORT_VEHICLE";
    public const int MaximumDataRows = 1000;
    public const long MaximumFileSize = 5 * 1024 * 1024;

    private static readonly string[] ExpectedHeaders =
    [
        "PlateNumber", "VehicleOwnerUserName", "OfficeTaxCode",
        "VehicleCode", "VehicleType", "Brand", "Model", "SeatCount", "Color",
        "ChassisNumber", "EngineNumber", "IsActive", "PermitNumber"
    ];

    public async Task<VehicleImportPreview> PreviewAsync(
        Stream excelStream,
        string fileName,
        string currentUserId,
        bool isOwner,
        CancellationToken ct = default)
    {
        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ hỗ trợ file Excel định dạng .xlsx.");

        VehicleImportPreview preview = new() { FileName = fileName };
        try
        {
            using XLWorkbook workbook = new(excelStream);
            var worksheet = workbook.Worksheets.FirstOrDefault(x =>
                string.Equals(x.Name, WorksheetName, StringComparison.OrdinalIgnoreCase));
            if (worksheet is null)
                throw new InvalidOperationException($"Không tìm thấy sheet '{WorksheetName}'. Vui lòng sử dụng đúng file mẫu.");

            ExcelImportUtility.ValidateHeaders(worksheet, ExpectedHeaders);
            for (var rowNumber = 2; rowNumber <= MaximumDataRows + 1; rowNumber++)
            {
                ct.ThrowIfCancellationRequested();
                var values = Enumerable.Range(1, ExpectedHeaders.Length)
                    .Select(column => ExcelImportUtility.ReadText(worksheet.Cell(rowNumber, column))).ToArray();
                if (values.All(string.IsNullOrWhiteSpace))
                    continue;

                VehicleImportRowPreview row = new()
                {
                    RowNumber = rowNumber,
                    PlateNumber = values[0].Trim().ToUpperInvariant(),
                    VehicleOwnerUserName = values[1].Trim(),
                    OfficeTaxCode = values[2].Trim(),
                    VehicleCode = ExcelImportUtility.NullIfWhiteSpace(values[3]),
                    VehicleType = ExcelImportUtility.NullIfWhiteSpace(values[4]),
                    Brand = ExcelImportUtility.NullIfWhiteSpace(values[5]),
                    Model = ExcelImportUtility.NullIfWhiteSpace(values[6]),
                    Color = ExcelImportUtility.NullIfWhiteSpace(values[8]),
                    ChassisNumber = ExcelImportUtility.NullIfWhiteSpace(values[9]),
                    EngineNumber = ExcelImportUtility.NullIfWhiteSpace(values[10]),
                    IsActive = true,
                    PermitNumber = ExcelImportUtility.NullIfWhiteSpace(values[12])
                };

                row.IsActive = ExcelImportUtility.ParseBoolean(values[11], true, "IsActive", row.Errors);
                if (!string.IsNullOrWhiteSpace(values[7]))
                {
                    if (int.TryParse(values[7], out var seats) && seats is >= 1 and <= 100)
                        row.SeatCount = seats;
                    else
                        ExcelImportUtility.AddError(row.Errors, "SeatCount phải là số nguyên từ 1 đến 100.");
                }

                ValidateRow(row);
                preview.Rows.Add(row);
            }
        }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Không thể đọc file Excel. File có thể bị hỏng, có mật khẩu hoặc không đúng mẫu.", ex);
        }

        MarkFileDuplicates(preview.Rows);
        await ResolveReferencesAndExistingDataAsync(preview.Rows, currentUserId, isOwner, ct);
        return preview;
    }

    public async Task<VehicleImportExecutionResult> ImportAsync(
        IReadOnlyCollection<VehicleImportRowPreview> rows,
        string currentUserId,
        bool isOwner,
        CancellationToken ct = default)
    {
        VehicleImportExecutionResult result = new() { RequestedCount = rows.Count(x => x.IsValid) };
        var managedOfficeIds = await officeAccessService.GetManagedOfficeIdsAsync(currentUserId, isOwner, ct);

        foreach (var row in rows.Where(x => x.IsValid))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await using var db = await factory.CreateDbContextAsync(ct);

                ApplicationUser? owner = null;
                if (!string.IsNullOrWhiteSpace(row.VehicleOwnerUserName))
                {
                    var normalizedName = userManager.NormalizeName(row.VehicleOwnerUserName);
                    var vehicleOwnerRoleId = await db.Roles.AsNoTracking()
                        .Where(x => x.Name == "VehicleOwner")
                        .Select(x => x.Id)
                        .FirstOrDefaultAsync(ct);
                    owner = await (
                        from user in db.Users
                        join userRole in db.UserRoles on user.Id equals userRole.UserId
                        where userRole.RoleId == vehicleOwnerRoleId &&
                              user.NormalizedUserName == normalizedName &&
                              !user.IsDeleted && user.IsActive && user.RegistrationStatus == "Approved"
                        select user).FirstOrDefaultAsync(ct);
                    if (owner is null)
                        throw new InvalidOperationException("Không tìm thấy tài khoản Chủ xe hợp lệ.");
                }

                var plate = row.PlateNumber.Trim().ToUpperInvariant();
                if (await db.Vehicles.IgnoreQueryFilters().AnyAsync(x => !x.IsDeleted && x.PlateNumber == plate, ct))
                    throw new InvalidOperationException("Biển số xe đã tồn tại.");

                var office = await db.CompanyProfiles.AsNoTracking()
                    .Where(x => x.TaxCode == row.OfficeTaxCode && x.IsActive && !x.IsDeleted)
                    .Select(x => new { x.Id, x.TaxCode })
                    .SingleOrDefaultAsync(ct);
                if (office is null)
                    throw new InvalidOperationException("Công ty/Văn phòng không tồn tại hoặc đã ngừng hoạt động.");
                if (!managedOfficeIds.Contains(office.Id))
                    throw new InvalidOperationException("Công ty/Văn phòng nằm ngoài phạm vi quản lý.");

                var now = DateTime.UtcNow;
                var vehicle = new Vehicle
                {
                    Id = Guid.NewGuid(),
                    PlateNumber = plate,
                    VehicleCode = N(row.VehicleCode),
                    PermitNumber = N(row.PermitNumber),
                    VehicleType = N(row.VehicleType),
                    Brand = N(row.Brand),
                    Model = N(row.Model),
                    SeatCount = row.SeatCount,
                    Color = N(row.Color),
                    ChassisNumber = N(row.ChassisNumber),
                    EngineNumber = N(row.EngineNumber),
                    IsActive = row.IsActive,
                    CreatedAt = now,
                    CreatedBy = currentUserId
                };
                if (owner is not null)
                    ApplyOwnerSnapshot(vehicle, owner);
                else
                    ClearOwnerSnapshot(vehicle);

                db.Vehicles.Add(vehicle);
                db.OfficeVehicles.Add(new OfficeVehicle
                {
                    VehicleId = vehicle.Id,
                    CompanyProfileId = office.Id,
                    IsPrimary = true,
                    IsActive = true,
                    AssignedFrom = now,
                    CreatedAt = now,
                    CreatedBy = currentUserId
                });

                if (owner is not null)
                {
                    db.DriverNotifications.Add(new DriverNotification
                    {
                        DriverId = owner.Id,
                        Type = "VehicleAssigned",
                        Title = "Bạn được gán phương tiện",
                        Message = $"Phương tiện {plate} đã được gán cho tài khoản Chủ xe của bạn.",
                        LinkUrl = "/vehicle-owner/vehicles",
                        RelatedVehicleId = vehicle.Id,
                        CreatedAt = now
                    });
                }

                await db.SaveChangesAsync(ct);
                result.ImportedVehicleIds.Add(vehicle.Id);
                result.ImportedCount++;
            }
            catch (Exception ex)
            {
                result.Errors.Add(new VehicleImportExecutionError
                {
                    RowNumber = row.RowNumber,
                    PlateNumber = row.PlateNumber,
                    Error = ex.Message
                });
            }
        }
        return result;
    }

    private async Task ResolveReferencesAndExistingDataAsync(
        IReadOnlyCollection<VehicleImportRowPreview> rows,
        string currentUserId,
        bool isOwner,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var managedOfficeIds = await officeAccessService.GetManagedOfficeIdsAsync(currentUserId, isOwner, ct);

        var taxCodes = rows.Where(x => !string.IsNullOrWhiteSpace(x.OfficeTaxCode))
            .Select(x => x.OfficeTaxCode)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var offices = await db.CompanyProfiles.AsNoTracking()
            .Where(x => taxCodes.Contains(x.TaxCode) && x.IsActive && !x.IsDeleted)
            .Select(x => new { x.Id, x.TaxCode })
            .ToListAsync(ct);
        var officeMap = offices.ToDictionary(x => x.TaxCode, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var normalizedNames = rows.Where(x => !string.IsNullOrWhiteSpace(x.VehicleOwnerUserName))
            .Select(x => userManager.NormalizeName(x.VehicleOwnerUserName))
            .Where(x => x is not null).Cast<string>().Distinct().ToList();
        var vehicleOwnerRoleId = await db.Roles.AsNoTracking().Where(x => x.Name == "VehicleOwner")
            .Select(x => x.Id).FirstOrDefaultAsync(ct);
        List<ApplicationUser> owners = normalizedNames.Count == 0
            ? []
            : await (
                from user in db.Users.AsNoTracking()
                join userRole in db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
                where userRole.RoleId == vehicleOwnerRoleId && user.NormalizedUserName != null &&
                      normalizedNames.Contains(user.NormalizedUserName) && !user.IsDeleted
                select user).ToListAsync(ct);
        var ownerMap = owners.Where(x => x.NormalizedUserName != null)
            .ToDictionary(x => x.NormalizedUserName!, StringComparer.OrdinalIgnoreCase);

        var plates = rows.Select(x => x.PlateNumber).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existingPlates = await db.Vehicles.IgnoreQueryFilters().AsNoTracking()
            .Where(x => !x.IsDeleted && plates.Contains(x.PlateNumber))
            .Select(x => x.PlateNumber).ToListAsync(ct);
        var existingPlateSet = existingPlates.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (existingPlateSet.Contains(row.PlateNumber))
                ExcelImportUtility.AddError(row.Errors, "Biển số xe đã tồn tại trong hệ thống.");

            if (!string.IsNullOrWhiteSpace(row.VehicleOwnerUserName))
            {
                var normalized = userManager.NormalizeName(row.VehicleOwnerUserName);
                if (normalized is null || !ownerMap.TryGetValue(normalized, out var owner))
                {
                    ExcelImportUtility.AddError(row.Errors, "Không tìm thấy tài khoản có Role Chủ xe.");
                }
                else
                {
                    row.VehicleOwnerName = owner.FullName;
                    if (!owner.IsActive || owner.RegistrationStatus != "Approved")
                        ExcelImportUtility.AddError(row.Errors, "Tài khoản Chủ xe đã khóa hoặc chưa được duyệt.");
                }
            }

            if (!officeMap.TryGetValue(row.OfficeTaxCode, out var officeId))
                ExcelImportUtility.AddError(row.Errors, $"Không tìm thấy Công ty/Văn phòng hoạt động có mã số thuế '{row.OfficeTaxCode}'.");
            else if (!managedOfficeIds.Contains(officeId))
                ExcelImportUtility.AddError(row.Errors, $"Không có quyền quản lý Công ty/Văn phòng mã số thuế '{row.OfficeTaxCode}'.");
        }
    }

    private static void ValidateRow(VehicleImportRowPreview row)
    {
        ExcelImportUtility.Required(row.Errors, row.PlateNumber, "PlateNumber");
        ExcelImportUtility.Required(row.Errors, row.OfficeTaxCode, "OfficeTaxCode");
        ExcelImportUtility.Required(row.Errors, row.PermitNumber, "PermitNumber");
        ExcelImportUtility.Maximum(row.Errors, row.PlateNumber, 20, "PlateNumber");
        ExcelImportUtility.Maximum(row.Errors, row.VehicleOwnerUserName, 256, "VehicleOwnerUserName");
        ExcelImportUtility.Maximum(row.Errors, row.OfficeTaxCode, 50, "OfficeTaxCode");
        ExcelImportUtility.Maximum(row.Errors, row.VehicleCode, 50, "VehicleCode");
        ExcelImportUtility.Maximum(row.Errors, row.PermitNumber, 50, "PermitNumber");
        ExcelImportUtility.Maximum(row.Errors, row.VehicleType, 100, "VehicleType");
        ExcelImportUtility.Maximum(row.Errors, row.Brand, 100, "Brand");
        ExcelImportUtility.Maximum(row.Errors, row.Model, 100, "Model");
        ExcelImportUtility.Maximum(row.Errors, row.Color, 50, "Color");
        ExcelImportUtility.Maximum(row.Errors, row.ChassisNumber, 100, "ChassisNumber");
        ExcelImportUtility.Maximum(row.Errors, row.EngineNumber, 100, "EngineNumber");
    }

    private static void MarkFileDuplicates(IReadOnlyCollection<VehicleImportRowPreview> rows)
    {
        var duplicates = rows.Where(x => !string.IsNullOrWhiteSpace(x.PlateNumber))
            .GroupBy(x => x.PlateNumber, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Where(x => duplicates.Contains(x.PlateNumber)))
            ExcelImportUtility.AddError(row.Errors, "PlateNumber bị trùng trong file import.");
    }

    private static void ApplyOwnerSnapshot(Vehicle vehicle, ApplicationUser owner)
    {
        vehicle.AssignedDriverId = owner.Id;
        vehicle.OwnerName = owner.FullName.Trim();
        vehicle.OwnerPhoneNumber = N(owner.PhoneNumber);
        vehicle.OwnerCitizenId = N(owner.CitizenId);
        vehicle.OwnerCitizenIdIssuedDate = owner.CitizenIdIssuedDate?.Date;
        vehicle.OwnerCitizenIdIssuedPlace = N(owner.CitizenIdIssuedPlace);
        vehicle.OwnerAddress = N(owner.Address);
    }

    private static void ClearOwnerSnapshot(Vehicle vehicle)
    {
        vehicle.AssignedDriverId = null;
        vehicle.OwnerName = string.Empty;
        vehicle.OwnerPhoneNumber = null;
        vehicle.OwnerCitizenId = null;
        vehicle.OwnerCitizenIdIssuedDate = null;
        vehicle.OwnerCitizenIdIssuedPlace = null;
        vehicle.OwnerAddress = null;
    }


    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class VehicleImportPreview
{
    public string FileName { get; set; } = string.Empty;
    public List<VehicleImportRowPreview> Rows { get; set; } = [];
    public int TotalRows => Rows.Count;
    public int ValidRows => Rows.Count(x => x.IsValid);
    public int InvalidRows => Rows.Count(x => !x.IsValid);
}

public sealed class VehicleImportRowPreview
{
    public int RowNumber { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string VehicleOwnerUserName { get; set; } = string.Empty;
    public string? VehicleOwnerName { get; set; }
    public string OfficeTaxCode { get; set; } = string.Empty;
    public string? VehicleCode { get; set; }
    public string? PermitNumber { get; set; }
    public string? VehicleType { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public int? SeatCount { get; set; }
    public string? Color { get; set; }
    public string? ChassisNumber { get; set; }
    public string? EngineNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> Errors { get; set; } = [];
    public bool IsValid => Errors.Count == 0;
    public string ErrorMessage => string.Join("; ", Errors);
}

public sealed class VehicleImportExecutionResult
{
    public int RequestedCount { get; set; }
    public int ImportedCount { get; set; }
    public int FailedCount => Errors.Count;
    public List<Guid> ImportedVehicleIds { get; set; } = [];
    public List<VehicleImportExecutionError> Errors { get; set; } = [];
}

public sealed class VehicleImportExecutionError
{
    public int RowNumber { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
