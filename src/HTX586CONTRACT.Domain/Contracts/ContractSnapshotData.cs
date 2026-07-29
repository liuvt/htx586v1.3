using System.Text.Json;
using HTX586CONTRACT.Domain.Companies;
using HTX586CONTRACT.Domain.Customers;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Domain.Enums;
using HTX586CONTRACT.Domain.Vehicles;

namespace HTX586CONTRACT.Domain.Contracts;

/// <summary>
/// Bản chụp độc lập của hợp đồng. Sau khi lưu, việc đổi xe, đổi Admin hoặc sửa
/// hồ sơ danh mục không làm thay đổi dữ liệu lịch sử của hợp đồng.
/// </summary>
public sealed class ContractSnapshotData
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public int Version { get; set; } = 3;
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
    public CompanySnapshot Company { get; set; } = new();
    public DriverSnapshot Driver { get; set; } = new();
    public CustomerSnapshot Customer { get; set; } = new();
    public VehicleSnapshot Vehicle { get; set; } = new();
    public List<PassengerSnapshot> Passengers { get; set; } = [];

    public static ContractSnapshotData CaptureManual(
        ApplicationUser admin,
        ApplicationUser driver,
        CustomerSnapshot customer,
        VehicleSnapshot vehicle,
        IEnumerable<PassengerSnapshot>? passengers = null,
        DateTime? capturedAtUtc = null)
        => new()
        {
            Version = 3,
            CapturedAtUtc = capturedAtUtc ?? DateTime.UtcNow,
            Company = CompanySnapshot.FromAdmin(admin),
            Driver = DriverSnapshot.FromUser(driver),
            Customer = customer,
            Vehicle = vehicle,
            Passengers = passengers?.OrderBy(x => x.SortOrder).ToList() ?? []
        };

    // Hàm cũ giữ để backfill hợp đồng lịch sử.
    public static ContractSnapshotData Capture(
        CompanyProfile company,
        ApplicationUser driver,
        Customer customer,
        Vehicle vehicle,
        DateTime? capturedAtUtc = null)
        => new()
        {
            Version = 1,
            CapturedAtUtc = capturedAtUtc ?? DateTime.UtcNow,
            Company = CompanySnapshot.FromLegacy(company),
            Driver = DriverSnapshot.FromUser(driver),
            Customer = new CustomerSnapshot
            {
                FullName = customer.FullName,
                OrganizationName = customer.OrganizationName,
                TaxCode = customer.TaxCode,
                PhoneNumber = customer.PhoneNumber,
                CitizenId = customer.CitizenId,
                CitizenIdIssuedDate = customer.CitizenIdIssuedDate,
                CitizenIdIssuedPlace = customer.CitizenIdIssuedPlace,
                Address = customer.Address,
                Email = customer.Email
            },
            Vehicle = VehicleSnapshot.FromLegacy(vehicle)
        };

    public static ContractSnapshotData CaptureLegacy(Contract contract)
    {
        var company = contract.CompanyProfile;
        var admin = contract.AdminAccount;
        var driver = contract.Driver;
        var customer = contract.Customer;
        var vehicle = contract.Vehicle;

        var snapshot = new ContractSnapshotData
        {
            Version = 1,
            CapturedAtUtc = contract.CompletedAt ?? contract.UpdatedAt ?? contract.CreatedAt,
            Company = admin is not null
                ? CompanySnapshot.FromAdmin(admin)
                : company is not null
                    ? CompanySnapshot.FromLegacy(company)
                    : new CompanySnapshot(),
            Driver = driver is not null ? DriverSnapshot.FromUser(driver) : new DriverSnapshot(),
            Customer = new CustomerSnapshot
            {
                FullName = First(contract.CustomerNameSnapshot, customer?.FullName),
                OrganizationName = customer?.OrganizationName,
                TaxCode = customer?.TaxCode,
                PhoneNumber = First(contract.CustomerPhoneSnapshot, customer?.PhoneNumber),
                CitizenId = First(contract.CustomerCitizenIdSnapshot, customer?.CitizenId),
                CitizenIdIssuedDate = customer?.CitizenIdIssuedDate,
                CitizenIdIssuedPlace = customer?.CitizenIdIssuedPlace,
                Address = First(contract.CustomerAddressSnapshot, customer?.Address),
                Email = customer?.Email,
                SignatureFileUrl = contract.Signatures.FirstOrDefault(x => x.Party == SignatureParty.Customer)?.SignatureFileUrl,
                SignatureHash = contract.Signatures.FirstOrDefault(x => x.Party == SignatureParty.Customer)?.SignatureHash,
                SignedAt = contract.Signatures.FirstOrDefault(x => x.Party == SignatureParty.Customer)?.ServerSignedAt
            },
            Vehicle = vehicle is not null ? VehicleSnapshot.FromLegacy(vehicle) : new VehicleSnapshot(),
            Passengers = contract.Passengers
                .OrderBy(x => x.SortOrder)
                .Select(x => new PassengerSnapshot
                {
                    SortOrder = x.SortOrder,
                    FullName = x.FullName,
                    BirthYear = x.BirthYear,
                    Note = x.Note
                })
                .ToList()
        };

        snapshot.Company.Name = First(contract.CompanyNameSnapshot, snapshot.Company.Name);
        snapshot.Company.TaxCode = First(contract.CompanyTaxCodeSnapshot, snapshot.Company.TaxCode);
        snapshot.Company.Address = First(contract.CompanyAddressSnapshot, snapshot.Company.Address);
        snapshot.Company.RepresentativeName = First(contract.CompanyRepresentativeSnapshot, snapshot.Company.RepresentativeName);
        snapshot.Company.RepresentativePosition = First(contract.CompanyRepresentativePositionSnapshot, snapshot.Company.RepresentativePosition);

        snapshot.Driver.UserId ??= contract.DriverId;
        snapshot.Driver.FullName = First(contract.DriverNameSnapshot, snapshot.Driver.FullName);
        snapshot.Driver.DriverLicenseNumber = First(contract.DriverLicenseNumberSnapshot, snapshot.Driver.DriverLicenseNumber);
        snapshot.Driver.DriverLicenseClass = First(contract.DriverLicenseClassSnapshot, snapshot.Driver.DriverLicenseClass);

        snapshot.Vehicle.PlateNumber = First(contract.VehiclePlateSnapshot, snapshot.Vehicle.PlateNumber);
        snapshot.Vehicle.Brand = First(contract.VehicleBrandSnapshot, snapshot.Vehicle.Brand);
        snapshot.Vehicle.OwnerName = First(contract.VehicleOwnerNameSnapshot, snapshot.Vehicle.OwnerName);
        snapshot.Vehicle.OwnerCitizenId = First(contract.VehicleOwnerCitizenIdSnapshot, snapshot.Vehicle.OwnerCitizenId);
        var ownerSignature = contract.Signatures.FirstOrDefault(x => x.Party == SignatureParty.VehicleOwner);
        if (ownerSignature is not null)
        {
            snapshot.Vehicle.OwnerSignatureFileUrl = ownerSignature.SignatureFileUrl;
            snapshot.Vehicle.OwnerSignatureHash = ownerSignature.SignatureHash;
            snapshot.Vehicle.OwnerSignedAt = ownerSignature.ServerSignedAt;
        }

        return snapshot;
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static ContractSnapshotData? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || string.Equals(json.Trim(), "{}", StringComparison.Ordinal))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ContractSnapshotData>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? First(params string?[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
}

public sealed class CompanySnapshot
{
    public string? AdminId { get; set; }
    public string? Name { get; set; }
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
        ? Name ?? string.Empty
        : $"{Name} - {BranchName}";

    public static CompanySnapshot FromAdmin(ApplicationUser admin) => new()
    {
        AdminId = admin.Id,
        Name = admin.CompanyName,
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

    public static CompanySnapshot FromLegacy(CompanyProfile company) => new()
    {
        Name = company.CompanyName,
        BranchName = company.BranchName,
        TaxCode = company.TaxCode,
        BusinessLicenseNumber = company.BusinessLicenseNumber,
        Address = company.Address,
        PhoneNumber = company.PhoneNumber,
        Email = company.Email,
        RepresentativeName = company.RepresentativeName,
        RepresentativePosition = company.RepresentativePosition,
        RepresentativeCitizenId = company.RepresentativeCitizenId,
        RepresentativeCitizenIdIssuedDate = company.RepresentativeCitizenIdIssuedDate,
        RepresentativeCitizenIdIssuedPlace = company.RepresentativeCitizenIdIssuedPlace,
        RepresentativeSignatureFileUrl = company.RepresentativeSignatureFileUrl,
        RepresentativeSignatureHash = company.RepresentativeSignatureHash,
        RepresentativeSignedAt = company.RepresentativeSignedAt
    };
}

public sealed class DriverSnapshot
{
    public string? UserId { get; set; }
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

    public static VehicleSnapshot FromLegacy(Vehicle vehicle) => new()
    {
        PlateNumber = vehicle.PlateNumber,
        VehicleCode = vehicle.VehicleCode,
        Brand = vehicle.Brand,
        Model = vehicle.Model,
        VehicleType = vehicle.VehicleType,
        SeatCount = vehicle.SeatCount,
        Color = vehicle.Color,
        ChassisNumber = vehicle.ChassisNumber,
        EngineNumber = vehicle.EngineNumber,
        OwnerName = vehicle.OwnerName,
        OwnerCitizenId = vehicle.OwnerCitizenId,
        OwnerCitizenIdIssuedDate = vehicle.OwnerCitizenIdIssuedDate,
        OwnerCitizenIdIssuedPlace = vehicle.OwnerCitizenIdIssuedPlace,
        OwnerAddress = vehicle.OwnerAddress,
        OwnerPhoneNumber = vehicle.OwnerPhoneNumber,
        OwnerSignatureFileUrl = vehicle.OwnerSignatureFileUrl,
        OwnerSignatureHash = vehicle.OwnerSignatureHash,
        OwnerSignedAt = vehicle.OwnerSignedAt
    };
}

public sealed class PassengerSnapshot
{
    public int SortOrder { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int? BirthYear { get; set; }
    public string? Note { get; set; }
}
