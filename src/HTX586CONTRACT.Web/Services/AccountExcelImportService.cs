using ClosedXML.Excel;
using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Application.Admins.AdminAccounts;
using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Web.Services;

public sealed class AccountExcelImportService(
    IDbContextFactory<ApplicationDbContext> factory,
    IAdminAccountService accountService,
    UserManager<ApplicationUser> userManager)
{
    public const string WorksheetName = "IMPORT_ACCOUNT";
    public const int MaximumDataRows = 1000;
    public const long MaximumFileSize = 5 * 1024 * 1024;

    private const string AdminRole = "Admin";
    private const string VehicleOwnerRole = "VehicleOwner";

    private static readonly string[] ExpectedHeaders =
    [
        "Role", "UserName", "Password", "FullName", "EmployeeCode", "PhoneNumber", "Email",
        "CitizenId", "CitizenIdIssuedDate", "CitizenIdIssuedPlace", "DateOfBirth", "Address",
        "AreaCode", "OfficeTaxCodes", "MustChangePassword"
    ];

    public async Task<AccountImportPreview> PreviewAsync(Stream excelStream, string fileName, CancellationToken ct = default)
    {
        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ hỗ trợ file Excel định dạng .xlsx.");

        AccountImportPreview preview = new() { FileName = fileName };
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
                var cells = Enumerable.Range(1, ExpectedHeaders.Length)
                    .Select(column => worksheet.Cell(rowNumber, column)).ToArray();
                if (cells.All(x => string.IsNullOrWhiteSpace(ExcelImportUtility.ReadText(x))))
                    continue;

                AccountImportRowPreview row = new() { RowNumber = rowNumber };
                var roleText = ExcelImportUtility.ReadText(cells[0]);
                row.Data.Role = ParseRole(roleText, row.Errors);
                row.Data.UserName = ExcelImportUtility.ReadText(cells[1]);
                row.Data.Password = string.IsNullOrWhiteSpace(ExcelImportUtility.ReadText(cells[2]))
                    ? "Htx@586"
                    : ExcelImportUtility.ReadText(cells[2]);
                row.Data.FullName = ExcelImportUtility.ReadText(cells[3]);
                row.Data.EmployeeCode = ExcelImportUtility.NullIfWhiteSpace(ExcelImportUtility.ReadText(cells[4]));
                row.Data.PhoneNumber = ExcelImportUtility.NullIfWhiteSpace(ExcelImportUtility.ReadText(cells[5]));
                row.Data.Email = ExcelImportUtility.NullIfWhiteSpace(ExcelImportUtility.ReadText(cells[6]));
                row.Data.CitizenId = ExcelImportUtility.NullIfWhiteSpace(ExcelImportUtility.ReadText(cells[7]));
                row.Data.CitizenIdIssuedDate = ExcelImportUtility.ParseDate(cells[8], "CitizenIdIssuedDate", row.Errors);
                row.Data.CitizenIdIssuedPlace = ExcelImportUtility.NullIfWhiteSpace(ExcelImportUtility.ReadText(cells[9]));
                row.Data.DateOfBirth = ExcelImportUtility.ParseDate(cells[10], "DateOfBirth", row.Errors);
                row.Data.Address = ExcelImportUtility.NullIfWhiteSpace(ExcelImportUtility.ReadText(cells[11]));
                row.Data.AreaCode = ExcelImportUtility.NullIfWhiteSpace(ExcelImportUtility.ReadText(cells[12]));
                row.OfficeTaxCodes = ExcelImportUtility.SplitCodes(ExcelImportUtility.ReadText(cells[13])).ToList();
                row.Data.MustChangePassword = ExcelImportUtility.ParseBoolean(
                    ExcelImportUtility.ReadText(cells[14]), true, "MustChangePassword", row.Errors);

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
        await ResolveReferencesAndExistingDataAsync(preview.Rows, ct);
        return preview;
    }

    public async Task<AccountImportExecutionResult> ImportAsync(
        IReadOnlyCollection<AccountImportRowPreview> rows,
        string createdByUserId,
        CancellationToken ct = default)
    {
        AccountImportExecutionResult result = new() { RequestedCount = rows.Count(x => x.IsValid) };
        foreach (var row in rows.Where(x => x.IsValid))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                row.Data.CreatedByUserId = createdByUserId;
                var created = await accountService.CreateAccountAsync(row.Data, ct);
                result.ImportedUserIds.Add(created.UserId);
                result.ImportedCount++;
            }
            catch (Exception ex)
            {
                result.Errors.Add(new AccountImportExecutionError
                {
                    RowNumber = row.RowNumber,
                    UserName = row.Data.UserName,
                    Error = ex.Message
                });
            }
        }
        return result;
    }

    private async Task ResolveReferencesAndExistingDataAsync(IReadOnlyCollection<AccountImportRowPreview> rows, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var taxCodes = rows.SelectMany(x => x.OfficeTaxCodes).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var offices = await db.CompanyProfiles.AsNoTracking()
            .Where(x => taxCodes.Contains(x.TaxCode) && x.IsActive && !x.IsDeleted)
            .Select(x => new { x.Id, x.TaxCode })
            .ToListAsync(ct);
        var officeMap = offices.ToDictionary(x => x.TaxCode, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var normalizedUserNames = rows.Where(x => !string.IsNullOrWhiteSpace(x.Data.UserName))
            .Select(x => userManager.NormalizeName(x.Data.UserName.Trim()))
            .Where(x => x is not null).Cast<string>().Distinct().ToList();
        var phones = rows.Where(x => !string.IsNullOrWhiteSpace(x.Data.PhoneNumber))
            .Select(x => x.Data.PhoneNumber!).Distinct().ToList();
        var employeeCodes = rows.Where(x => !string.IsNullOrWhiteSpace(x.Data.EmployeeCode))
            .Select(x => x.Data.EmployeeCode!).Distinct().ToList();

        var existing = await db.Users.AsNoTracking()
            .Where(x =>
                (x.NormalizedUserName != null && normalizedUserNames.Contains(x.NormalizedUserName)) ||
                 (x.PhoneNumber != null && phones.Contains(x.PhoneNumber)) ||
                 (x.EmployeeCode != null && employeeCodes.Contains(x.EmployeeCode)))
            .Select(x => new { x.NormalizedUserName, x.PhoneNumber, x.EmployeeCode })
            .ToListAsync(ct);
        var existingNames = existing.Where(x => x.NormalizedUserName != null).Select(x => x.NormalizedUserName!).ToHashSet();
        var existingPhones = existing.Where(x => x.PhoneNumber != null).Select(x => x.PhoneNumber!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingEmployees = existing.Where(x => x.EmployeeCode != null).Select(x => x.EmployeeCode!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var resolved = new HashSet<Guid>();
            foreach (var code in row.OfficeTaxCodes)
            {
                if (officeMap.TryGetValue(code, out var officeId)) resolved.Add(officeId);
                else ExcelImportUtility.AddError(row.Errors, $"Không tìm thấy Công ty/Văn phòng hoạt động có mã số thuế '{code}'.");
            }
            row.Data.OfficeIds = resolved;

            if (row.Data.Role == AdminRole && resolved.Count == 0)
                ExcelImportUtility.AddError(row.Errors, "Tài khoản Quản lý phải có ít nhất một OfficeTaxCodes.");
            if (row.Data.Role == VehicleOwnerRole && row.OfficeTaxCodes.Count > 0)
                ExcelImportUtility.AddError(row.Errors, "Tài khoản Chủ xe không gán trực tiếp Công ty/Văn phòng; hãy để trống OfficeTaxCodes.");

            var normalized = userManager.NormalizeName(row.Data.UserName.Trim());
            if (normalized is not null && existingNames.Contains(normalized))
                ExcelImportUtility.AddError(row.Errors, "Tên đăng nhập đã tồn tại trong hệ thống.");
            if (!string.IsNullOrWhiteSpace(row.Data.PhoneNumber) && existingPhones.Contains(row.Data.PhoneNumber))
                ExcelImportUtility.AddError(row.Errors, "Số điện thoại đã được sử dụng.");
            if (!string.IsNullOrWhiteSpace(row.Data.EmployeeCode) && existingEmployees.Contains(row.Data.EmployeeCode))
                ExcelImportUtility.AddError(row.Errors, "Mã nhân viên đã tồn tại.");
        }
    }

    private static void ValidateRow(AccountImportRowPreview row)
    {
        ExcelImportUtility.Required(row.Errors, row.Data.Role, "Role");
        ExcelImportUtility.Required(row.Errors, row.Data.UserName, "UserName");
        ExcelImportUtility.Required(row.Errors, row.Data.Password, "Password");
        ExcelImportUtility.Required(row.Errors, row.Data.FullName, "FullName");
        ExcelImportUtility.Required(row.Errors, row.Data.PhoneNumber, "PhoneNumber");
        ExcelImportUtility.Maximum(row.Errors, row.Data.UserName, 256, "UserName");
        ExcelImportUtility.Maximum(row.Errors, row.Data.FullName, 200, "FullName");
        ExcelImportUtility.Maximum(row.Errors, row.Data.EmployeeCode, 30, "EmployeeCode");
        ExcelImportUtility.Maximum(row.Errors, row.Data.Email, 256, "Email");
        ExcelImportUtility.Maximum(row.Errors, row.Data.CitizenId, 30, "CitizenId");
        ExcelImportUtility.Maximum(row.Errors, row.Data.CitizenIdIssuedPlace, 300, "CitizenIdIssuedPlace");
        ExcelImportUtility.Maximum(row.Errors, row.Data.Address, 500, "Address");
        ExcelImportUtility.Maximum(row.Errors, row.Data.AreaCode, 20, "AreaCode");

        if (!string.IsNullOrWhiteSpace(row.Data.Email) && !ExcelImportUtility.IsValidEmail(row.Data.Email))
            ExcelImportUtility.AddError(row.Errors, "Email không đúng định dạng.");
        if (row.Data.Password.Length < 6 || !row.Data.Password.Any(char.IsDigit))
            ExcelImportUtility.AddError(row.Errors, "Password phải có ít nhất 6 ký tự và chứa ít nhất một chữ số.");

        if (!string.IsNullOrWhiteSpace(row.Data.PhoneNumber))
        {
            if (VietnamPhoneNumber.TryNormalize(row.Data.PhoneNumber, out var phone)) row.Data.PhoneNumber = phone;
            else ExcelImportUtility.AddError(row.Errors, VietnamPhoneNumber.ValidationMessage);
        }

        if (row.Data.Role == VehicleOwnerRole)
        {
            ExcelImportUtility.Required(row.Errors, row.Data.CitizenId, "CitizenId");
            if (!row.Data.CitizenIdIssuedDate.HasValue) ExcelImportUtility.AddError(row.Errors, "Chủ xe phải có CitizenIdIssuedDate.");
            ExcelImportUtility.Required(row.Errors, row.Data.CitizenIdIssuedPlace, "CitizenIdIssuedPlace");
            ExcelImportUtility.Required(row.Errors, row.Data.Address, "Address");
        }
    }

    private static string ParseRole(string value, ICollection<string> errors)
    {
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized is "ADMIN" or "QUẢN LÝ" or "QUAN LY") return AdminRole;
        if (normalized is "VEHICLEOWNER" or "CHỦ XE" or "CHU XE") return VehicleOwnerRole;
        ExcelImportUtility.AddError(errors, "Role chỉ nhận 'Quản lý' hoặc 'Chủ xe'.");
        return value.Trim();
    }

    private void MarkFileDuplicates(IReadOnlyCollection<AccountImportRowPreview> rows)
    {
        MarkDuplicate(rows, x => userManager.NormalizeName(x.Data.UserName.Trim()) ?? x.Data.UserName.Trim(), "UserName bị trùng trong file.");
        MarkDuplicate(rows.Where(x => !string.IsNullOrWhiteSpace(x.Data.PhoneNumber)).ToList(), x => x.Data.PhoneNumber!, "PhoneNumber bị trùng trong file.");
        MarkDuplicate(rows.Where(x => !string.IsNullOrWhiteSpace(x.Data.EmployeeCode)).ToList(), x => x.Data.EmployeeCode!, "EmployeeCode bị trùng trong file.");
    }

    private static void MarkDuplicate(IReadOnlyCollection<AccountImportRowPreview> rows, Func<AccountImportRowPreview, string> keySelector, string message)
    {
        var duplicates = rows.GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Count() > 1)
            .Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Where(x => duplicates.Contains(keySelector(x))))
            ExcelImportUtility.AddError(row.Errors, message);
    }
}

public sealed class AccountImportPreview
{
    public string FileName { get; set; } = string.Empty;
    public List<AccountImportRowPreview> Rows { get; set; } = [];
    public int TotalRows => Rows.Count;
    public int ValidRows => Rows.Count(x => x.IsValid);
    public int InvalidRows => Rows.Count(x => !x.IsValid);
}

public sealed class AccountImportRowPreview
{
    public int RowNumber { get; set; }
    public CreateAdminAccountRequest Data { get; set; } = new();
    public List<string> OfficeTaxCodes { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public bool IsValid => Errors.Count == 0;
    public string ErrorMessage => string.Join("; ", Errors);
    public string RoleDisplay => Data.Role == "Admin" ? "Quản lý" : Data.Role == "VehicleOwner" ? "Chủ xe" : Data.Role;
}

public sealed class AccountImportExecutionResult
{
    public int RequestedCount { get; set; }
    public int ImportedCount { get; set; }
    public int FailedCount => Errors.Count;
    public List<string> ImportedUserIds { get; set; } = [];
    public List<AccountImportExecutionError> Errors { get; set; } = [];
}

public sealed class AccountImportExecutionError
{
    public int RowNumber { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
