using System.Globalization;
using System.Net.Mail;
using ClosedXML.Excel;

namespace HTX586CONTRACT.Web.Services;

public static class ExcelImportUtility
{
    private static readonly string[] SupportedDateFormats =
    [
        "dd/MM/yyyy", "d/M/yyyy", "dd/M/yyyy", "d/MM/yyyy",
        "yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy"
    ];

    public static string ReadText(IXLCell cell) => cell.GetFormattedString().Trim();

    public static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static DateTime? ParseDate(IXLCell cell, string fieldName, ICollection<string> errors)
    {
        var text = ReadText(cell);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var culture = CultureInfo.GetCultureInfo("vi-VN");
        if (DateTime.TryParseExact(text, SupportedDateFormats, culture,
                DateTimeStyles.AllowWhiteSpaces, out var exactDate) ||
            DateTime.TryParse(text, culture, DateTimeStyles.AllowWhiteSpaces, out exactDate))
            return exactDate.Date;

        errors.Add($"{fieldName} phải có định dạng dd/MM/yyyy.");
        return null;
    }

    public static bool ParseBoolean(string value, bool defaultValue, string fieldName, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        return value.Trim().ToUpperInvariant() switch
        {
            "TRUE" or "1" or "YES" or "CÓ" or "CO" or "ACTIVE" or "HOẠT ĐỘNG" or "HOAT DONG" => true,
            "FALSE" or "0" or "NO" or "KHÔNG" or "KHONG" or "INACTIVE" or "NGỪNG" or "NGUNG" => false,
            _ => AddBooleanError(defaultValue, fieldName, errors)
        };
    }

    public static IReadOnlyList<string> SplitCodes(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([';', ',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    public static bool IsValidEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;
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

    public static void Required(ICollection<string> errors, string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            AddError(errors, $"Thiếu trường bắt buộc {fieldName}.");
    }

    public static void Maximum(ICollection<string> errors, string? value, int maximumLength, string fieldName)
    {
        if (value?.Length > maximumLength)
            AddError(errors, $"{fieldName} vượt quá {maximumLength} ký tự.");
    }

    public static void AddError(ICollection<string> errors, string message)
    {
        if (!errors.Contains(message, StringComparer.OrdinalIgnoreCase))
            errors.Add(message);
    }

    public static void ValidateHeaders(IXLWorksheet worksheet, IReadOnlyList<string> expectedHeaders)
    {
        List<string> errors = [];
        for (var column = 1; column <= expectedHeaders.Count; column++)
        {
            var actual = worksheet.Cell(1, column).GetString().Trim();
            var expected = expectedHeaders[column - 1];
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                errors.Add($"cột {column}: cần '{expected}', hiện là '{actual}'");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("Tiêu đề cột không đúng mẫu: " + string.Join("; ", errors) + ".");
    }

    private static bool AddBooleanError(bool fallback, string fieldName, ICollection<string> errors)
    {
        errors.Add($"{fieldName} chỉ nhận TRUE hoặc FALSE.");
        return fallback;
    }
}
