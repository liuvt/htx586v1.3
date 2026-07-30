namespace HTX586CONTRACT.Application.Admins.DriverAccounts;

public sealed class DriverAccountDto
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? AdminId { get; set; }
    public string? CompanyName { get; set; }
    public string? CitizenId { get; set; }
    public string? DriverLicenseNumber { get; set; }
    public string? DriverLicenseClass { get; set; }
    public string? DriverSignatureFileUrl { get; set; }
    public bool HasDriverSignature => !string.IsNullOrWhiteSpace(DriverSignatureFileUrl);
    public string DriverSignatureStatusText => HasDriverSignature ? "Đã có chữ ký" : "Chưa có chữ ký";
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
