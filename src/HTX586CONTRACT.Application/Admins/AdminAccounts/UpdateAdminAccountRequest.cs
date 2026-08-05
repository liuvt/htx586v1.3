namespace HTX586CONTRACT.Application.Admins.AdminAccounts;

public sealed class UpdateAdminAccountRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin";
    public HashSet<Guid> OfficeIds { get; set; } = [];
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
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public string? UpdatedByUserId { get; set; }
}
