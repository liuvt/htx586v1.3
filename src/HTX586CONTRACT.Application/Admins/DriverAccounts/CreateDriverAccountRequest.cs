namespace HTX586CONTRACT.Application.Admins.DriverAccounts;

/// <summary>
/// Yêu cầu tạo tài khoản VehicleOwner. Tài khoản này không gán trực tiếp vào
/// công ty/văn phòng; phạm vi đơn vị được xác định theo từng xe được cấp.
/// </summary>
public sealed class CreateDriverAccountRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = "Htx@586";
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? CitizenId { get; set; }
    public DateTime? CitizenIdIssuedDate { get; set; }
    public string? CitizenIdIssuedPlace { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? AreaCode { get; set; }
    public string? DriverLicenseNumber { get; set; }
    public string? DriverLicenseClass { get; set; }
    public DateTime? DriverLicenseIssuedDate { get; set; }
    public DateTime? DriverLicenseExpiryDate { get; set; }
    public bool MustChangePassword { get; set; } = true;
    public string? CreatedByUserId { get; set; }
}
