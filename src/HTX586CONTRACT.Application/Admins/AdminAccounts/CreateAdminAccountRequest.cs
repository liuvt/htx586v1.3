namespace HTX586CONTRACT.Application.Admins.AdminAccounts;

public sealed class CreateAdminAccountRequest
{
    public Guid CompanyProfileId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public bool MustChangePassword { get; set; } = true;
}
