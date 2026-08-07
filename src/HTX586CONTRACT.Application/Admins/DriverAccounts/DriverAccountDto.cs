namespace HTX586CONTRACT.Application.Admins.DriverAccounts;

public sealed class DriverAccountDto
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? CitizenId { get; set; }
    public string? DriverLicenseNumber { get; set; }
    public string? DriverLicenseClass { get; set; }
    public string? DriverSignatureFileUrl { get; set; }
    public bool DriverSignatureIsActive { get; set; }
    public string DriverSignatureStatusText =>
        DriverSignatureIsActive ? "Đã có chân ký Chủ xe" : "Chưa có chân ký Chủ xe";
    public DateTime? DriverSignatureInactiveAt { get; set; }
    public int VehicleCount { get; set; }
    public int SignedVehicleCount { get; set; }
    public string VehiclePlates { get; set; } = string.Empty;
    public string CompanyNames { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
