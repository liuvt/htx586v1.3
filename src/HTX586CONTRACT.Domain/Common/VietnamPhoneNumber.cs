using System.Text.RegularExpressions;

namespace HTX586CONTRACT.Domain.Common;

public static class VietnamPhoneNumber
{
    private static readonly Regex MobileRegex = new(
        @"^0(?:3|5|7|8|9)\d{8}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public const string ValidationMessage =
        "Số điện thoại phải là số di động Việt Nam hợp lệ gồm 10 chữ số, bắt đầu bằng 03, 05, 07, 08 hoặc 09.";

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (trimmed.Any(ch =>
                !char.IsDigit(ch) &&
                !char.IsWhiteSpace(ch) &&
                ch != '+' && ch != '-' && ch != '.' && ch != '(' && ch != ')'))
            return false;

        var digits = new string(trimmed.Where(char.IsDigit).ToArray());

        if (digits.StartsWith("0084", StringComparison.Ordinal) && digits.Length == 13)
            digits = "0" + digits[4..];
        else if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length == 11)
            digits = "0" + digits[2..];

        if (!MobileRegex.IsMatch(digits))
            return false;

        normalized = digits;
        return true;
    }

    public static string NormalizeOrThrow(string? value)
    {
        if (TryNormalize(value, out var normalized))
            return normalized;

        throw new InvalidOperationException(ValidationMessage);
    }
}
