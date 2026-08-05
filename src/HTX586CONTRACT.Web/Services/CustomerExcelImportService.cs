using System.Text.RegularExpressions;
using ClosedXML.Excel;
using HTX586CONTRACT.Domain.Customers;
using HTX586CONTRACT.Domain.Enums;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Web.Services;

public sealed partial class CustomerExcelImportService(IDbContextFactory<ApplicationDbContext> factory)
{
    public const string WorksheetName = "IMPORT_CUSTOMER";
    public const int MaximumDataRows = 1000;
    public const long MaximumFileSize = 5 * 1024 * 1024;

    private static readonly string[] ExpectedHeaders =
    [
        "Type", "FullName", "OrganizationName", "TaxCode", "PhoneNumber", "CitizenId",
        "CitizenIdIssuedDate", "CitizenIdIssuedPlace", "DateOfBirth", "Address", "Email"
    ];

    public async Task<CustomerImportPreview> PreviewAsync(
        Stream excelStream,
        string fileName,
        string createdByUserId,
        CancellationToken ct = default)
    {
        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ hỗ trợ file Excel định dạng .xlsx.");

        CustomerImportPreview preview = new() { FileName = fileName };
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

                var rawPhone = ExcelImportUtility.ReadText(cells[4]);
                CustomerImportRowPreview row = new()
                {
                    RowNumber = rowNumber,
                    Type = CustomerType.Individual,
                    FullName = ExcelImportUtility.ReadText(cells[1]),
                    OrganizationName = ExcelImportUtility.NullIfWhiteSpace(ExcelImportUtility.ReadText(cells[2])),
                    TaxCode = ExcelImportUtility.NullIfWhiteSpace(ExcelImportUtility.ReadText(cells[3])),
                    PhoneNumber = NormalizeCustomerPhone(rawPhone),
                    CitizenId = ExcelImportUtility.NullIfWhiteSpace(ExcelImportUtility.ReadText(cells[5])),
                    CitizenIdIssuedPlace = ExcelImportUtility.NullIfWhiteSpace(ExcelImportUtility.ReadText(cells[7])),
                    Address = ExcelImportUtility.NullIfWhiteSpace(ExcelImportUtility.ReadText(cells[9])),
                    Email = ExcelImportUtility.NullIfWhiteSpace(ExcelImportUtility.ReadText(cells[10]))
                };
                row.Type = ParseType(ExcelImportUtility.ReadText(cells[0]), row.Errors);
                if (rawPhone.Any(ch => !char.IsDigit(ch) && !char.IsWhiteSpace(ch) && ch != '+' && ch != '-' && ch != '.' && ch != '(' && ch != ')'))
                    ExcelImportUtility.AddError(row.Errors, "PhoneNumber chứa ký tự không hợp lệ.");
                row.CitizenIdIssuedDate = ExcelImportUtility.ParseDate(cells[6], "CitizenIdIssuedDate", row.Errors);
                row.DateOfBirth = ExcelImportUtility.ParseDate(cells[8], "DateOfBirth", row.Errors);
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
        await MarkExistingAsync(preview.Rows, createdByUserId, ct);
        return preview;
    }

    public async Task<CustomerImportExecutionResult> ImportAsync(
        IReadOnlyCollection<CustomerImportRowPreview> rows,
        string createdByUserId,
        CancellationToken ct = default)
    {
        CustomerImportExecutionResult result = new() { RequestedCount = rows.Count(x => x.IsValid) };
        foreach (var row in rows.Where(x => x.IsValid))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await using var db = await factory.CreateDbContextAsync(ct);
                var exists = await db.Customers.IgnoreQueryFilters().AsNoTracking().AnyAsync(x =>
                    !x.IsDeleted && x.CreatedByDriverId == createdByUserId && x.PhoneNumber == row.PhoneNumber, ct);
                if (exists)
                    throw new InvalidOperationException("Số điện thoại khách hàng đã tồn tại trong dữ liệu do tài khoản này tạo.");

                var now = DateTime.UtcNow;
                var customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    Type = row.Type,
                    FullName = row.FullName.Trim(),
                    OrganizationName = N(row.OrganizationName),
                    TaxCode = N(row.TaxCode),
                    PhoneNumber = row.PhoneNumber,
                    CitizenId = N(row.CitizenId),
                    CitizenIdIssuedDate = row.CitizenIdIssuedDate?.Date,
                    CitizenIdIssuedPlace = N(row.CitizenIdIssuedPlace),
                    DateOfBirth = row.DateOfBirth?.Date,
                    Address = N(row.Address),
                    Email = N(row.Email),
                    CreatedByDriverId = createdByUserId,
                    CreatedAt = now,
                    CreatedBy = createdByUserId,
                    UpdatedAt = now,
                    UpdatedBy = createdByUserId
                };
                db.Customers.Add(customer);
                await db.SaveChangesAsync(ct);
                result.ImportedCustomerIds.Add(customer.Id);
                result.ImportedCount++;
            }
            catch (Exception ex)
            {
                result.Errors.Add(new CustomerImportExecutionError
                {
                    RowNumber = row.RowNumber,
                    CustomerName = row.DisplayName,
                    Error = ex.Message
                });
            }
        }
        return result;
    }

    private async Task MarkExistingAsync(
        IReadOnlyCollection<CustomerImportRowPreview> rows,
        string createdByUserId,
        CancellationToken ct)
    {
        var phones = rows.Select(x => x.PhoneNumber).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        if (phones.Count == 0) return;
        await using var db = await factory.CreateDbContextAsync(ct);
        var existing = await db.Customers.IgnoreQueryFilters().AsNoTracking()
            .Where(x => !x.IsDeleted && x.CreatedByDriverId == createdByUserId && phones.Contains(x.PhoneNumber))
            .Select(x => x.PhoneNumber).ToListAsync(ct);
        var set = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Where(x => set.Contains(x.PhoneNumber)))
            ExcelImportUtility.AddError(row.Errors, "Số điện thoại khách hàng đã tồn tại trong dữ liệu do tài khoản này tạo.");
    }

    private static void ValidateRow(CustomerImportRowPreview row)
    {
        ExcelImportUtility.Required(row.Errors, row.FullName, "FullName");
        ExcelImportUtility.Required(row.Errors, row.PhoneNumber, "PhoneNumber");
        if (row.Type == CustomerType.Organization)
            ExcelImportUtility.Required(row.Errors, row.OrganizationName, "OrganizationName");
        ExcelImportUtility.Maximum(row.Errors, row.FullName, 200, "FullName");
        ExcelImportUtility.Maximum(row.Errors, row.PhoneNumber, 20, "PhoneNumber");
        ExcelImportUtility.Maximum(row.Errors, row.CitizenId, 20, "CitizenId");
        ExcelImportUtility.Maximum(row.Errors, row.Address, 500, "Address");
        ExcelImportUtility.Maximum(row.Errors, row.Email, 200, "Email");
        if (!ExcelImportUtility.IsValidEmail(row.Email))
            ExcelImportUtility.AddError(row.Errors, "Email không đúng định dạng.");
        if (string.IsNullOrWhiteSpace(row.PhoneNumber) || !CustomerPhoneRegex().IsMatch(row.PhoneNumber))
            ExcelImportUtility.AddError(row.Errors, "PhoneNumber phải có từ 8 đến 15 chữ số.");
    }

    private static CustomerType ParseType(string value, ICollection<string> errors)
    {
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized is "INDIVIDUAL" or "CÁ NHÂN" or "CA NHAN" or "1") return CustomerType.Individual;
        if (normalized is "ORGANIZATION" or "TỔ CHỨC" or "TO CHUC" or "ĐƠN VỊ" or "DON VI" or "ĐƠN VỊ/TỔ CHỨC" or "DON VI/TO CHUC" or "2") return CustomerType.Organization;
        ExcelImportUtility.AddError(errors, "Type chỉ nhận 'Cá nhân' hoặc 'Tổ chức'.");
        return CustomerType.Individual;
    }

    private static string NormalizeCustomerPhone(string value)
    {
        var trimmed = value.Trim();
        var hasPlus = trimmed.StartsWith('+');
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        return hasPlus ? "+" + digits : digits;
    }

    private static void MarkFileDuplicates(IReadOnlyCollection<CustomerImportRowPreview> rows)
    {
        var duplicates = rows.Where(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .GroupBy(x => x.PhoneNumber, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Where(x => duplicates.Contains(x.PhoneNumber)))
            ExcelImportUtility.AddError(row.Errors, "PhoneNumber bị trùng trong file import.");
    }

    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"^\+?\d{8,15}$", RegexOptions.CultureInvariant)]
    private static partial Regex CustomerPhoneRegex();
}

public sealed class CustomerImportPreview
{
    public string FileName { get; set; } = string.Empty;
    public List<CustomerImportRowPreview> Rows { get; set; } = [];
    public int TotalRows => Rows.Count;
    public int ValidRows => Rows.Count(x => x.IsValid);
    public int InvalidRows => Rows.Count(x => !x.IsValid);
}

public sealed class CustomerImportRowPreview
{
    public int RowNumber { get; set; }
    public CustomerType Type { get; set; } = CustomerType.Individual;
    public string FullName { get; set; } = string.Empty;
    public string? OrganizationName { get; set; }
    public string? TaxCode { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? CitizenId { get; set; }
    public DateTime? CitizenIdIssuedDate { get; set; }
    public string? CitizenIdIssuedPlace { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? Email { get; set; }
    public List<string> Errors { get; set; } = [];
    public bool IsValid => Errors.Count == 0;
    public string ErrorMessage => string.Join("; ", Errors);
    public string DisplayName => Type == CustomerType.Organization && !string.IsNullOrWhiteSpace(OrganizationName)
        ? OrganizationName : FullName;
    public string TypeDisplay => Type == CustomerType.Organization ? "Tổ chức" : "Cá nhân";
}

public sealed class CustomerImportExecutionResult
{
    public int RequestedCount { get; set; }
    public int ImportedCount { get; set; }
    public int FailedCount => Errors.Count;
    public List<Guid> ImportedCustomerIds { get; set; } = [];
    public List<CustomerImportExecutionError> Errors { get; set; } = [];
}

public sealed class CustomerImportExecutionError
{
    public int RowNumber { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
