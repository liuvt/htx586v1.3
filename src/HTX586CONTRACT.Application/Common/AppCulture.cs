using System.Globalization;

namespace HTX586CONTRACT.Application.Common;

/// <summary>
/// Các thiết lập văn hóa dùng chung cho trường ngày và giờ trên giao diện.
/// </summary>
public static class AppCulture
{
    public static CultureInfo Vietnamese { get; } = CultureInfo.GetCultureInfo("vi-VN");
}
