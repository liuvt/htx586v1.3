using System.Globalization;
using System.Text;

namespace HTX586CONTRACT.Application.Contracts;

/// <summary>
/// Tự động ánh xạ mã Đơn vị, số Hợp đồng dịch vụ vận tải (Hợp đồng phụ) và mẫu điều khoản
/// tương ứng với từng Đơn vị (Cao Sơn Car, Mekong Xanh, HTX 586 hoặc đơn vị bất kỳ).
/// </summary>
public static class SubContractHelper
{
    public static bool IsCaoSonUnit(string? companyName, string? taxCode)
    {
        if (!string.IsNullOrWhiteSpace(taxCode) &&
            taxCode.Trim().StartsWith("0319340385", StringComparison.OrdinalIgnoreCase))
            return true;

        var normalized = RemoveDiacritics(companyName).ToUpperInvariant();
        return normalized.Contains("CAO SON", StringComparison.Ordinal);
    }

    public static bool IsMekongXanhUnit(string? companyName, string? taxCode)
    {
        if (!string.IsNullOrWhiteSpace(taxCode) &&
            taxCode.Trim().StartsWith("0319595866", StringComparison.OrdinalIgnoreCase))
            return true;

        var normalized = RemoveDiacritics(companyName).ToUpperInvariant();
        return normalized.Contains("MEKONG XANH", StringComparison.Ordinal);
    }

    public static string ResolveUnitCode(string? companyName, string? taxCode)
    {
        if (IsCaoSonUnit(companyName, taxCode))
            return "HTXCSRCN";

        if (IsMekongXanhUnit(companyName, taxCode))
            return "HTXMKXCN";

        var normalized = RemoveDiacritics(companyName).ToUpperInvariant();
        if (normalized.Contains("586", StringComparison.Ordinal))
            return "HTX586CN";

        var words = normalized
            .Split([' ', '-', ',', '.', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
        var initials = new StringBuilder();
        foreach (var word in words)
        {
            if (char.IsLetterOrDigit(word[0]))
                initials.Append(word[0]);
        }

        var code = initials.ToString();
        if (string.IsNullOrWhiteSpace(code))
            return "HTXCN";

        return code.Length > 10 ? code[..10] : code;
    }

    public static string BuildSubContractNumber(
        string? mainContractNumber,
        DateTime createdAtUtc,
        string? companyName,
        string? companyTaxCode)
    {
        var vietnamDate = createdAtUtc.Kind == DateTimeKind.Utc
            ? createdAtUtc.AddHours(7)
            : createdAtUtc;
        var datePrefix = vietnamDate.ToString("ddMM", CultureInfo.InvariantCulture);
        var sequence = ExtractSequence(mainContractNumber);
        var unitCode = ResolveUnitCode(companyName, companyTaxCode);
        return $"{datePrefix}. {sequence:00}{unitCode}";
    }

    public static (DateTime EffectiveFrom, DateTime EffectiveTo) GetEffectivePeriod(DateTime createdAtUtc)
    {
        var vietnamDate = (createdAtUtc.Kind == DateTimeKind.Utc
            ? createdAtUtc.AddHours(7)
            : createdAtUtc).Date;
        return (vietnamDate, vietnamDate.AddYears(4));
    }

    private static int ExtractSequence(string? mainContractNumber)
    {
        if (string.IsNullOrWhiteSpace(mainContractNumber))
            return 1;

        var trimmed = mainContractNumber.Trim();
        if (trimmed.StartsWith("TMP-", StringComparison.OrdinalIgnoreCase))
            return 1;

        var lastDash = trimmed.LastIndexOf('-');
        if (lastDash >= 0 && lastDash < trimmed.Length - 1)
        {
            var suffix = trimmed[(lastDash + 1)..];
            if (int.TryParse(suffix, out var parsed) && parsed > 0)
                return parsed;
        }

        return 1;
    }

    private static string RemoveDiacritics(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c switch
                {
                    'đ' => 'd',
                    'Đ' => 'D',
                    _ => c
                });
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
