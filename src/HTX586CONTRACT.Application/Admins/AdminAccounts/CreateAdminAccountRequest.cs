namespace HTX586CONTRACT.Application.Admins.AdminAccounts;

public sealed class CreateAdminAccountRequest
{
    public string Role { get; set; } = "Admin";
    public HashSet<Guid> OfficeIds { get; set; } = [];
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
    public bool MustChangePassword { get; set; } = true;
    public string? CreatedByUserId { get; set; }
}
