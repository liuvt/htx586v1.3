namespace HTX586CONTRACT.Application.Admins.DriverAccounts;

/// <summary>
/// Đăng ký tài khoản chờ duyệt. Không yêu cầu công ty, giấy phép lái xe hay chữ ký;
/// chữ ký được thiết lập một lần theo từng xe sau khi tài khoản được cấp xe.
/// </summary>
public sealed class SelfRegisterDriverRequest
{
    public Guid? CompanyProfileId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    // Các trường cũ giữ lại để tương thích binary/source, không bắt buộc và không dùng khi đăng ký.
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
