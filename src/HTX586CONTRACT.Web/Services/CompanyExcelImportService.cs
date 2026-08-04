using System.Globalization;
using System.Net.Mail;
using ClosedXML.Excel;
using HTX586CONTRACT.Application.Admins.CompanyProfiles;
using HTX586CONTRACT.Domain.Companies;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Web.Services;

/// <summary>
/// Đọc và import Công ty/Văn phòng từ file Excel theo mẫu
/// wwwroot/templates/Template_Import_Company_HTX586.xlsx.
/// </summary>
public sealed class CompanyExcelImportService(
    IDbContextFactory<ApplicationDbContext> factory)
{
    public const string WorksheetName = "IMPORT_COMPANY";
    public const int MaximumDataRows = 1000;
    public const long MaximumFileSize = 5 * 1024 * 1024;

    private static readonly string[] ExpectedHeaders =
    [
        "CompanyName",
        "BranchName",
        "TaxCode",
        "BusinessLicenseNumber",
        "Address",
        "PhoneNumber",
        "Email",
        "RepresentativeName",
        "RepresentativePosition",
        "RepresentativeCitizenId",
        "RepresentativeCitizenIdIssuedDate",
        "RepresentativeCitizenIdIssuedPlace",
        "BankAccountNumber",
        "BankName",
        "IsActive"
    ];

    private static readonly string[] SupportedDateFormats =
    [
        "dd/MM/yyyy",
        "d/M/yyyy",
        "dd/M/yyyy",
        "d/MM/yyyy",
        "yyyy-MM-dd",
        "MM/dd/yyyy",
        "M/d/yyyy"
    ];

    public async Task<CompanyImportPreview> PreviewAsync(
        Stream excelStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(excelStream);

        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ hỗ trợ file Excel định dạng .xlsx.");

        CompanyImportPreview preview = new()
        {
            FileName = fileName
        };

        try
        {
            using XLWorkbook workbook = new(excelStream);
            var worksheet = workbook.Worksheets.FirstOrDefault(x =>
                string.Equals(x.Name, WorksheetName, StringComparison.OrdinalIgnoreCase));

            if (worksheet is null)
                throw new InvalidOperationException(
                    $"Không tìm thấy sheet '{WorksheetName}'. Vui lòng sử dụng đúng file mẫu của hệ thống.");

            ValidateHeaders(worksheet);

            for (var rowNumber = 2; rowNumber <= MaximumDataRows + 1; rowNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var values = Enumerable.Range(1, ExpectedHeaders.Length)
                    .Select(column => ReadText(worksheet.Cell(rowNumber, column)))
                    .ToArray();

                if (values.All(string.IsNullOrWhiteSpace))
                    continue;

                CompanyImportRowPreview row = new()
                {
                    RowNumber = rowNumber,
                    Data = new CreateCompanyProfileRequest
                    {
                        CompanyName = values[0],
                        BranchName = NullIfWhiteSpace(values[1]),
                        TaxCode = values[2],
                        BusinessLicenseNumber = NullIfWhiteSpace(values[3]),
                        Address = values[4],
                        PhoneNumber = NullIfWhiteSpace(values[5]),
                        Email = NullIfWhiteSpace(values[6]),
                        RepresentativeName = values[7],
                        RepresentativePosition = NullIfWhiteSpace(values[8]),
                        RepresentativeCitizenId = values[9],
                        RepresentativeCitizenIdIssuedPlace = NullIfWhiteSpace(values[11]),
                        BankAccountNumber = NullIfWhiteSpace(values[12]),
                        BankName = NullIfWhiteSpace(values[13]),
                        IsActive = true
                    }
                };

                row.Data.RepresentativeCitizenIdIssuedDate = ParseDate(
                    values[10],
                    row.Errors);
                row.Data.IsActive = ParseActive(values[14], row.Errors);

                ValidateRow(row);
                preview.Rows.Add(row);
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Không thể đọc file Excel. File có thể bị hỏng, đang đặt mật khẩu hoặc không đúng mẫu import.",
                ex);
        }

        MarkDuplicatesInsideFile(preview.Rows);
        await MarkExistingTaxCodesAsync(preview.Rows, cancellationToken);

        return preview;
    }

    public async Task<CompanyImportExecutionResult> ImportAsync(
        IReadOnlyCollection<CompanyImportRowPreview> previewRows,
        string createdByUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(createdByUserId))
            throw new InvalidOperationException("Không xác định được tài khoản Owner thực hiện import.");

        var validRows = previewRows.Where(x => x.IsValid).ToList();
        CompanyImportExecutionResult result = new()
        {
            RequestedCount = validRows.Count
        };

        if (validRows.Count == 0)
            return result;

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var taxCodes = validRows
            .Select(x => x.Data.TaxCode.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingTaxCodes = await db.CompanyProfiles
            .AsNoTracking()
            .Where(x => taxCodes.Contains(x.TaxCode))
            .Select(x => x.TaxCode)
            .ToListAsync(cancellationToken);

        var blockedTaxCodes = existingTaxCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        var pendingEntities = new List<(CompanyImportRowPreview Row, CompanyProfile Entity)>();

        foreach (var row in validRows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var taxCode = row.Data.TaxCode.Trim();
            if (blockedTaxCodes.Contains(taxCode))
            {
                result.Errors.Add(new CompanyImportExecutionError
                {
                    RowNumber = row.RowNumber,
                    CompanyName = row.Data.CompanyName,
                    TaxCode = taxCode,
                    Error = "Mã số thuế đã tồn tại tại thời điểm import."
                });
                continue;
            }

            var entity = Map(row.Data, createdByUserId.Trim(), now);
            db.CompanyProfiles.Add(entity);
            pendingEntities.Add((row, entity));
            blockedTaxCodes.Add(taxCode);
        }

        if (pendingEntities.Count == 0)
            return result;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(
                "Không thể lưu dữ liệu import. Hãy kiểm tra mã số thuế trùng hoặc dữ liệu vượt quá độ dài cho phép.",
                ex);
        }

        result.ImportedCount = pendingEntities.Count;
        result.ImportedIds.AddRange(pendingEntities.Select(x => x.Entity.Id));
        return result;
    }

    private static void ValidateHeaders(IXLWorksheet worksheet)
    {
        List<string> errors = [];

        for (var column = 1; column <= ExpectedHeaders.Length; column++)
        {
            var actual = worksheet.Cell(1, column).GetString().Trim();
            var expected = ExpectedHeaders[column - 1];

            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                errors.Add($"cột {column}: cần '{expected}', hiện là '{actual}'");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Tiêu đề cột không đúng mẫu: " + string.Join("; ", errors) + ".");
        }
    }

    private async Task MarkExistingTaxCodesAsync(
        IReadOnlyCollection<CompanyImportRowPreview> rows,
        CancellationToken cancellationToken)
    {
        var taxCodes = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.Data.TaxCode))
            .Select(x => x.Data.TaxCode.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (taxCodes.Count == 0)
            return;

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.CompanyProfiles
            .AsNoTracking()
            .Where(x => taxCodes.Contains(x.TaxCode))
            .Select(x => x.TaxCode)
            .ToListAsync(cancellationToken);

        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Where(x => existingSet.Contains(x.Data.TaxCode.Trim())))
            AddError(row, "Mã số thuế đã tồn tại trong hệ thống.");
    }

    private static void MarkDuplicatesInsideFile(IReadOnlyCollection<CompanyImportRowPreview> rows)
    {
        var duplicateTaxCodes = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.Data.TaxCode))
            .GroupBy(x => x.Data.TaxCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows.Where(x => duplicateTaxCodes.Contains(x.Data.TaxCode.Trim())))
            AddError(row, "Mã số thuế bị trùng trong file import.");
    }

    private static void ValidateRow(CompanyImportRowPreview row)
    {
        var data = row.Data;

        Required(row, data.CompanyName, "CompanyName/Tên đơn vị");
        Required(row, data.TaxCode, "TaxCode/Mã số thuế");
        Required(row, data.Address, "Address/Địa chỉ");
        Required(row, data.RepresentativeName, "RepresentativeName/Người đại diện");
        Required(row, data.RepresentativeCitizenId, "RepresentativeCitizenId/CCCD người đại diện");

        Maximum(row, data.CompanyName, 300, "CompanyName");
        Maximum(row, data.BranchName, 300, "BranchName");
        Maximum(row, data.TaxCode, 50, "TaxCode");
        Maximum(row, data.BusinessLicenseNumber, 100, "BusinessLicenseNumber");
        Maximum(row, data.Address, 500, "Address");
        Maximum(row, data.PhoneNumber, 20, "PhoneNumber");
        Maximum(row, data.Email, 256, "Email");
        Maximum(row, data.RepresentativeName, 200, "RepresentativeName");
        Maximum(row, data.RepresentativePosition, 100, "RepresentativePosition");
        Maximum(row, data.RepresentativeCitizenId, 30, "RepresentativeCitizenId");
        Maximum(row, data.RepresentativeCitizenIdIssuedPlace, 300, "RepresentativeCitizenIdIssuedPlace");
        Maximum(row, data.BankAccountNumber, 50, "BankAccountNumber");
        Maximum(row, data.BankName, 200, "BankName");

        if (!string.IsNullOrWhiteSpace(data.Email) && !IsValidEmail(data.Email))
            AddError(row, "Email không đúng định dạng.");
    }

    private static DateTime? ParseDate(string value, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var vietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");
        if (DateTime.TryParseExact(
                value.Trim(),
                SupportedDateFormats,
                vietnameseCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var exactDate) ||
            DateTime.TryParse(
                value.Trim(),
                vietnameseCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out exactDate))
        {
            return exactDate.Date;
        }

        errors.Add("RepresentativeCitizenIdIssuedDate phải có định dạng dd/MM/yyyy.");
        return null;
    }

    private static bool ParseActive(string value, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        return value.Trim().ToUpperInvariant() switch
        {
            "TRUE" or "1" or "YES" or "CÓ" or "CO" or "ACTIVE" => true,
            "FALSE" or "0" or "NO" or "KHÔNG" or "KHONG" or "INACTIVE" => false,
            _ => AddBooleanError(errors)
        };
    }

    private static bool AddBooleanError(ICollection<string> errors)
    {
        errors.Add("IsActive chỉ nhận TRUE hoặc FALSE.");
        return true;
    }

    private static CompanyProfile Map(
        CreateCompanyProfileRequest request,
        string createdByUserId,
        DateTime createdAt)
    {
        return new CompanyProfile
        {
            Id = Guid.NewGuid(),
            CompanyName = request.CompanyName.Trim(),
            BranchName = NullIfWhiteSpace(request.BranchName),
            TaxCode = request.TaxCode.Trim(),
            BusinessLicenseNumber = NullIfWhiteSpace(request.BusinessLicenseNumber),
            Address = request.Address.Trim(),
            PhoneNumber = NullIfWhiteSpace(request.PhoneNumber),
            Email = NullIfWhiteSpace(request.Email),
            RepresentativeName = request.RepresentativeName.Trim(),
            RepresentativePosition = NullIfWhiteSpace(request.RepresentativePosition),
            RepresentativeCitizenId = request.RepresentativeCitizenId.Trim(),
            RepresentativeCitizenIdIssuedDate = request.RepresentativeCitizenIdIssuedDate,
            RepresentativeCitizenIdIssuedPlace = NullIfWhiteSpace(request.RepresentativeCitizenIdIssuedPlace),
            BankAccountNumber = NullIfWhiteSpace(request.BankAccountNumber),
            BankName = NullIfWhiteSpace(request.BankName),
            IsActive = request.IsActive,
            CreatedAt = createdAt,
            CreatedByUserId = createdByUserId
        };
    }

    private static string ReadText(IXLCell cell) => cell.GetFormattedString().Trim();

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Required(CompanyImportRowPreview row, string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            AddError(row, $"Thiếu trường bắt buộc {fieldName}.");
    }

    private static void Maximum(
        CompanyImportRowPreview row,
        string? value,
        int maximumLength,
        string fieldName)
    {
        if (value?.Length > maximumLength)
            AddError(row, $"{fieldName} vượt quá {maximumLength} ký tự.");
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            var address = new MailAddress(value.Trim());
            return string.Equals(address.Address, value.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void AddError(CompanyImportRowPreview row, string message)
    {
        if (!row.Errors.Contains(message, StringComparer.OrdinalIgnoreCase))
            row.Errors.Add(message);
    }
}

public sealed class CompanyImportPreview
{
    public string FileName { get; set; } = string.Empty;
    public List<CompanyImportRowPreview> Rows { get; set; } = [];
    public int TotalRows => Rows.Count;
    public int ValidRows => Rows.Count(x => x.IsValid);
    public int InvalidRows => Rows.Count(x => !x.IsValid);
}

public sealed class CompanyImportRowPreview
{
    public int RowNumber { get; set; }
    public CreateCompanyProfileRequest Data { get; set; } = new();
    public List<string> Errors { get; set; } = [];
    public bool IsValid => Errors.Count == 0;
    public string ErrorMessage => string.Join("; ", Errors);
}

public sealed class CompanyImportExecutionResult
{
    public int RequestedCount { get; set; }
    public int ImportedCount { get; set; }
    public int FailedCount => Errors.Count;
    public List<Guid> ImportedIds { get; set; } = [];
    public List<CompanyImportExecutionError> Errors { get; set; } = [];
}

public sealed class CompanyImportExecutionError
{
    public int RowNumber { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TaxCode { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
