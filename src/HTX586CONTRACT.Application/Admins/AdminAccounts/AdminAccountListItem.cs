namespace HTX586CONTRACT.Application.Admins.AdminAccounts;

public sealed class AdminAccountListItem
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public string OfficeNames { get; set; } = string.Empty;
    public int VehicleCount { get; set; }
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
}
