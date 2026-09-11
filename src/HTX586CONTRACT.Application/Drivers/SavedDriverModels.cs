namespace HTX586CONTRACT.Application.Drivers;

public sealed class SavedDriverDto
{
    public string DriverLicenseNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DriverLicenseClass { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime? DeletedAt { get; set; }
}

public sealed class SaveSavedDriverRequest
{
    public string DriverLicenseNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DriverLicenseClass { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}

public sealed record SaveSavedDriverResult(bool Succeeded, string Message);
