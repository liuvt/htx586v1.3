namespace HTX586CONTRACT.Application.Admins.DriverAccounts;

/// <summary>
/// Đăng ký tài khoản chờ duyệt. Không yêu cầu công ty, giấy phép lái xe hay chữ ký;
/// chân ký Chủ xe được lưu một lần trên tài khoản và dùng chung cho các xe được cấp.
/// </summary>
public sealed class SelfRegisterDriverRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    // Hồ sơ Chủ xe: CCCD và ngày cấp là bắt buộc ngay khi đăng ký;
    // các thông tin còn lại có thể bổ sung sau khi tài khoản được duyệt.
    public DateTime? DateOfBirth { get; set; }
    public string? AreaCode { get; set; }
    public string? Address { get; set; }
    public string? CitizenId { get; set; }
    public DateTime? CitizenIdIssuedDate { get; set; }
    public string? CitizenIdIssuedPlace { get; set; }
    public string? DriverLicenseNumber { get; set; }
    public string? DriverLicenseClass { get; set; }
    public DateTime? DriverLicenseIssuedDate { get; set; }
    public DateTime? DriverLicenseExpiryDate { get; set; }
    public string SignatureDataUrl { get; set; } = string.Empty;
}
