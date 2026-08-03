namespace HTX586CONTRACT.Application.Admins.DriverAccounts;

public sealed class DriverRegistrationRequestDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public Guid? CompanyProfileId { get; set; }
    public string? CompanyName { get; set; }
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
    public string? DriverSignatureFileUrl { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ViewedAt { get; set; }
    public bool IsViewed => ViewedAt.HasValue;
}
