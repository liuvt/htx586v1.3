namespace HTX586CONTRACT.Application.Admins.DriverAccounts;

public sealed class DriverAccountDetailDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? AdminId { get; set; }
    public Guid? CompanyProfileId { get; set; }
    public string? CompanyName { get; set; }
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
    public string? DriverSignatureFileUrl { get; set; }
    public DateTime? DriverSignedAt { get; set; }
    public bool DriverSignatureIsActive { get; set; }
    public string DriverSignatureStatusText => DriverSignatureIsActive ? "Đang sử dụng" : "Chưa có chữ ký";
    public DateTime? DriverSignatureInactiveAt { get; set; }
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
