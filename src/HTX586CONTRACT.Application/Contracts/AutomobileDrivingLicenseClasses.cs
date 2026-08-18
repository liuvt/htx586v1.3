namespace HTX586CONTRACT.Application.Contracts;

/// <summary>
/// Các hạng GPLX dùng cho người trực tiếp điều khiển ô tô trên hợp đồng.
/// Bao gồm hệ hạng hiện hành từ 01/01/2025 và một số hạng ô tô cũ vẫn còn
/// giá trị sử dụng theo thời hạn ghi trên GPLX đã cấp trước 01/01/2025.
/// </summary>
public static class AutomobileDrivingLicenseClasses
{
    public static readonly IReadOnlyList<string> Current =
    [
        "B",
        "C1",
        "C",
        "D1",
        "D2",
        "D",
        "BE",
        "C1E",
        "CE",
        "D1E",
        "D2E",
        "DE"
    ];

    public static readonly IReadOnlyList<string> LegacyStillValid =
    [
        "B1 số tự động",
        "B1",
        "B2",
        "E",
        "FB2",
        "FC",
        "FD",
        "FE"
    ];

    public static readonly IReadOnlyList<string> All = Current
        .Concat(LegacyStillValid)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();


    public static string DisplayName(string value)
        => LegacyStillValid.Contains(value, StringComparer.OrdinalIgnoreCase)
            ? $"{value} (GPLX cũ còn hiệu lực)"
            : value;

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           All.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
}
