using System.Globalization;
using System.Text;

namespace HTX586CONTRACT.Application.Contracts;

public sealed class SubContractUnitProfile
{
    public string UnitCode { get; init; } = string.Empty;
    public string UnitHeaderTitle { get; init; } = string.Empty;
    public string OfficeLocationName { get; init; } = string.Empty;
    public string PartyAName { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string LicenseLabel { get; init; } = string.Empty;
    public string LicenseNumber { get; init; } = string.Empty;
    public string LicenseIssuedDateText { get; init; } = string.Empty;
    public string LicenseIssuedPlace { get; init; } = string.Empty;
    public string TaxCode { get; init; } = string.Empty;
    public string RepresentativeName { get; init; } = string.Empty;
    public string RepresentativePosition { get; init; } = string.Empty;
    public string RepresentativeCitizenId { get; init; } = string.Empty;
    public string RepresentativeCitizenIdIssuedDateText { get; init; } = string.Empty;
    public string RepresentativeCitizenIdIssuedPlace { get; init; } = string.Empty;
    public string SignatureRepresentativeName { get; init; } = string.Empty;
    public bool IsCaoSonPreset { get; init; }
}

/// <summary>
/// Tự động ánh xạ mã Đơn vị, thông tin Hợp đồng phụ cứng riêng cho từng khu HTX
/// (CN HTX Công nghệ VT Cao Sơn Car tại Cần Thơ, CN HTX Cộng đồng Mekong Xanh Cần Thơ, và các đơn vị khác).
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
            (taxCode.Trim().StartsWith("0319595866", StringComparison.OrdinalIgnoreCase) ||
             taxCode.Trim().StartsWith("0316700428", StringComparison.OrdinalIgnoreCase)))
            return true;

        var normalized = RemoveDiacritics(companyName).ToUpperInvariant();
        return normalized.Contains("MEKONG XANH", StringComparison.Ordinal);
    }

    /// <summary>
    /// Trả về thông tin Bên A cứng trong Hợp đồng phụ tương ứng với khu HTX / Đơn vị.
    /// </summary>
    public static SubContractUnitProfile ResolveFixedUnitProfile(
        string? companyName,
        string? taxCode,
        string? fallbackAddress = null,
        string? fallbackPhone = null,
        string? fallbackLicenseNumber = null,
        string? fallbackLicensePlace = null,
        string? fallbackRepName = null,
        string? fallbackRepPosition = null,
        string? fallbackRepCitizenId = null,
        DateTime? fallbackRepCitizenIssuedDate = null,
        string? fallbackRepCitizenIssuedPlace = null)
    {
        if (IsCaoSonUnit(companyName, taxCode))
        {
            return new SubContractUnitProfile
            {
                UnitCode = "HTXCSRCN",
                UnitHeaderTitle = "CN HTX CÔNG NGHỆ VT CAO SƠN CAR TẠI CẦN THƠ",
                OfficeLocationName = "CN Hợp tác xã công nghệ vận tải Cao Sơn Car tại Cần Thơ",
                PartyAName = "CN HTX CÔNG NGHỆ VT CAO SƠN CAR TẠI CẦN THƠ",
                Address = "Số 47 đường số 12, KTĐC Thới Nhựt 2, Phường Tân An, Thành phố Cần Thơ",
                PhoneNumber = "0925351111",
                LicenseLabel = "Giấy chứng nhận đăng ký kinh doanh số",
                LicenseNumber = "922643479/GPKDVT",
                LicenseIssuedDateText = "07 tháng 08 năm 2026",
                LicenseIssuedPlace = "Sở Xây dựng Thành phố Cần Thơ",
                TaxCode = "0319340385-001",
                RepresentativeName = "Nguyễn Trọng Hậu",
                RepresentativePosition = "Giám đốc.",
                RepresentativeCitizenId = "82203018675",
                RepresentativeCitizenIdIssuedDateText = "19/01/2026",
                RepresentativeCitizenIdIssuedPlace = "Bộ Công An",
                SignatureRepresentativeName = "NGUYỄN TRỌNG HẬU",
                IsCaoSonPreset = true
            };
        }

        if (IsMekongXanhUnit(companyName, taxCode))
        {
            return new SubContractUnitProfile
            {
                UnitCode = "HTXMKXCN",
                UnitHeaderTitle = "CHI NHÁNH HỢP TÁC XÃ CỘNG ĐỒNG MEKONG XANH CẦN THƠ",
                OfficeLocationName = "CHI NHÁNH HỢP TÁC XÃ CỘNG ĐỒNG MEKONG XANH CẦN THƠ",
                PartyAName = "CHI NHÁNH HỢP TÁC XÃ CỘNG ĐỒNG MEKONG XANH CẦN THƠ",
                Address = "Số 4/48 Đường Cách Mạng Tháng 8, Phường Cái Khế, Thành Phố Cần Thơ",
                PhoneNumber = "0911772265",
                LicenseLabel = "Giấy phép kinh doanh vận tải số",
                LicenseNumber = "922641667/GPKDVT",
                LicenseIssuedDateText = "16 tháng 07 năm 2026",
                LicenseIssuedPlace = "Sở Xây dựng Thành phố Cần Thơ",
                TaxCode = "0319595866-001",
                RepresentativeName = "Phạm Hoàng Nam",
                RepresentativePosition = "Giám đốc",
                RepresentativeCitizenId = "091092007059",
                RepresentativeCitizenIdIssuedDateText = "22/11/2022",
                RepresentativeCitizenIdIssuedPlace = "Cục Cảnh Sát QLHC về TTXH",
                SignatureRepresentativeName = "PHẠM HOÀNG NAM",
                IsCaoSonPreset = false
            };
        }

        var unitName = string.IsNullOrWhiteSpace(companyName) ? "HỢP TÁC XÃ VẬN TẢI" : companyName.Trim();
        var repName = string.IsNullOrWhiteSpace(fallbackRepName) ? "..." : fallbackRepName.Trim();
        return new SubContractUnitProfile
        {
            UnitCode = ResolveUnitCode(companyName, taxCode),
            UnitHeaderTitle = unitName.ToUpper(CultureInfo.GetCultureInfo("vi-VN")),
            OfficeLocationName = unitName,
            PartyAName = unitName.ToUpper(CultureInfo.GetCultureInfo("vi-VN")),
            Address = string.IsNullOrWhiteSpace(fallbackAddress) ? "..." : fallbackAddress.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(fallbackPhone) ? "..." : fallbackPhone.Trim(),
            LicenseLabel = "Giấy phép kinh doanh vận tải số",
            LicenseNumber = string.IsNullOrWhiteSpace(fallbackLicenseNumber) ? "..." : fallbackLicenseNumber.Trim(),
            LicenseIssuedDateText = string.Empty,
            LicenseIssuedPlace = string.IsNullOrWhiteSpace(fallbackLicensePlace) ? "Sở Xây dựng Thành phố Cần Thơ" : fallbackLicensePlace.Trim(),
            TaxCode = string.IsNullOrWhiteSpace(taxCode) ? "..." : taxCode.Trim(),
            RepresentativeName = repName,
            RepresentativePosition = string.IsNullOrWhiteSpace(fallbackRepPosition) ? "Giám đốc" : fallbackRepPosition.Trim(),
            RepresentativeCitizenId = string.IsNullOrWhiteSpace(fallbackRepCitizenId) ? "..." : fallbackRepCitizenId.Trim(),
            RepresentativeCitizenIdIssuedDateText = fallbackRepCitizenIssuedDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "...",
            RepresentativeCitizenIdIssuedPlace = string.IsNullOrWhiteSpace(fallbackRepCitizenIssuedPlace) ? "Cục Cảnh Sát QLHC về TTXH" : fallbackRepCitizenIssuedPlace.Trim(),
            SignatureRepresentativeName = repName.ToUpper(CultureInfo.GetCultureInfo("vi-VN")),
            IsCaoSonPreset = false
        };
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
