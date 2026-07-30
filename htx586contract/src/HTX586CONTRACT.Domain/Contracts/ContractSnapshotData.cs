using System.Text.Json;
using HTX586CONTRACT.Domain.Identity;

namespace HTX586CONTRACT.Domain.Contracts;

public sealed class ContractSnapshotData
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        IgnoreReadOnlyProperties = true
    };

    public int Version { get; set; } = 1;
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
    public CompanySnapshot Company { get; set; } = new();
    public DriverSnapshot Driver { get; set; } = new();
    public CustomerSnapshot Customer { get; set; } = new();
    public VehicleSnapshot Vehicle { get; set; } = new();
    public List<PassengerSnapshot> Passengers { get; set; } = [];

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static ContractSnapshotData? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return null;
        try { return JsonSerializer.Deserialize<ContractSnapshotData>(json, JsonOptions); }
        catch (JsonException) { return null; }
    }

    public static ContractSnapshotData Capture(
        ApplicationUser admin,
        ApplicationUser driver,
        CustomerSnapshot customer,
        VehicleSnapshot vehicle,
        IEnumerable<PassengerSnapshot>? passengers = null)
        => new()
        {
            CapturedAtUtc = DateTime.UtcNow,
            Company = CompanySnapshot.FromAdmin(admin),
            Driver = DriverSnapshot.FromUser(driver),
            Customer = customer,
            Vehicle = vehicle,
            Passengers = passengers?.OrderBy(x => x.SortOrder).ToList() ?? []
        };
}

public sealed class CompanySnapshot
{
    public string AdminId { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? BranchName { get; set; }
    public string? TaxCode { get; set; }
    public string? BusinessLicenseNumber { get; set; }
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? RepresentativeName { get; set; }
    public string? RepresentativePosition { get; set; }
    public string? RepresentativeCitizenId { get; set; }
    public DateTime? RepresentativeCitizenIdIssuedDate { get; set; }
    public string? RepresentativeCitizenIdIssuedPlace { get; set; }
    public string? RepresentativeSignatureFileUrl { get; set; }
    public string? RepresentativeSignatureHash { get; set; }
    public DateTime? RepresentativeSignedAt { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(BranchName)
        ? CompanyName ?? string.Empty
        : $"{CompanyName} - {BranchName}";

    public static CompanySnapshot FromAdmin(ApplicationUser admin) => new()
    {
        AdminId = admin.Id,
        CompanyName = admin.CompanyName,
        BranchName = admin.CompanyBranchName,
        TaxCode = admin.CompanyTaxCode,
        BusinessLicenseNumber = admin.CompanyBusinessLicenseNumber,
        Address = admin.CompanyAddress,
        PhoneNumber = admin.CompanyPhoneNumber,
        Email = admin.CompanyEmail,
        RepresentativeName = admin.CompanyRepresentativeName,
        RepresentativePosition = admin.CompanyRepresentativePosition,
        RepresentativeCitizenId = admin.CompanyRepresentativeCitizenId,
        RepresentativeCitizenIdIssuedDate = admin.CompanyRepresentativeCitizenIdIssuedDate,
        RepresentativeCitizenIdIssuedPlace = admin.CompanyRepresentativeCitizenIdIssuedPlace,
        RepresentativeSignatureFileUrl = admin.CompanySignatureFileUrl,
        RepresentativeSignatureHash = admin.CompanySignatureHash,
        RepresentativeSignedAt = admin.CompanySignedAt
    };
}

public sealed class DriverSnapshot
{
    public string UserId { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? CitizenId { get; set; }
    public DateTime? CitizenIdIssuedDate { get; set; }
    public string? CitizenIdIssuedPlace { get; set; }
    public string? Address { get; set; }
    public string? AreaCode { get; set; }
    public string? DriverLicenseNumber { get; set; }
    public string? DriverLicenseClass { get; set; }
    public DateTime? DriverLicenseIssuedDate { get; set; }
    public DateTime? DriverLicenseExpiryDate { get; set; }
    public string? SignatureFileUrl { get; set; }
    public string? SignatureHash { get; set; }
    public DateTime? SignedAt { get; set; }

    public static DriverSnapshot FromUser(ApplicationUser driver) => new()
    {
        UserId = driver.Id,
        FullName = driver.FullName,
        PhoneNumber = driver.PhoneNumber,
        CitizenId = driver.CitizenId,
        CitizenIdIssuedDate = driver.CitizenIdIssuedDate,
        CitizenIdIssuedPlace = driver.CitizenIdIssuedPlace,
        Address = driver.Address,
        AreaCode = driver.AreaCode,
        DriverLicenseNumber = driver.DriverLicenseNumber,
        DriverLicenseClass = driver.DriverLicenseClass,
        DriverLicenseIssuedDate = driver.DriverLicenseIssuedDate,
        DriverLicenseExpiryDate = driver.DriverLicenseExpiryDate,
        SignatureFileUrl = driver.DriverSignatureFileUrl,
        SignatureHash = driver.DriverSignatureHash,
        SignedAt = driver.DriverSignedAt
    };
}

public sealed class CustomerSnapshot
{
    public string? FullName { get; set; }
    public string? OrganizationName { get; set; }
    public string? TaxCode { get; set; }
    public string? PhoneNumber { get; set; }
    public string? CitizenId { get; set; }
    public DateTime? CitizenIdIssuedDate { get; set; }
    public string? CitizenIdIssuedPlace { get; set; }
    public string? Address { get; set; }
    public string? Email { get; set; }
    public string? SignatureFileUrl { get; set; }
    public string? SignatureHash { get; set; }
    public DateTime? SignedAt { get; set; }
}

public sealed class VehicleSnapshot
{
    public string? PlateNumber { get; set; }
    public string? VehicleCode { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? VehicleType { get; set; }
    public int? SeatCount { get; set; }
    public string? Color { get; set; }
    public string? ChassisNumber { get; set; }
    public string? EngineNumber { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerCitizenId { get; set; }
    public DateTime? OwnerCitizenIdIssuedDate { get; set; }
    public string? OwnerCitizenIdIssuedPlace { get; set; }
    public string? OwnerAddress { get; set; }
    public string? OwnerPhoneNumber { get; set; }
    public string? OwnerSignatureFileUrl { get; set; }
    public string? OwnerSignatureHash { get; set; }
    public DateTime? OwnerSignedAt { get; set; }

    public string BrandModel => string.Join(" ", new[] { Brand, Model, VehicleType }
        .Where(x => !string.IsNullOrWhiteSpace(x)));
}

public sealed class PassengerSnapshot
{
    public int SortOrder { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int? BirthYear { get; set; }
    public string? Note { get; set; }
}
