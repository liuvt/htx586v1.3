using System.Text.Json;
using HTX586CONTRACT.Domain.Companies;
using HTX586CONTRACT.Domain.Customers;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Domain.Vehicles;

namespace HTX586CONTRACT.Domain.Contracts;

/// <summary>
/// Bản chụp bất biến của toàn bộ dữ liệu danh mục được dùng trên hợp đồng.
/// Dữ liệu này được ghi vào ContractDataJson khi tạo/cập nhật hợp đồng và không
/// được đọc lại từ Company/Vehicle/Driver/Customer sau khi hợp đồng hoàn tất.
/// </summary>
public sealed class ContractSnapshotData
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public int Version { get; set; } = 1;
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
    public CompanySnapshot Company { get; set; } = new();
    public DriverSnapshot Driver { get; set; } = new();
    public CustomerSnapshot Customer { get; set; } = new();
    public VehicleSnapshot Vehicle { get; set; } = new();

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
            Company = new CompanySnapshot
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
            },
            Driver = new DriverSnapshot
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
                SignatureFileUrl = null,
                SignatureHash = null,
                SignedAt = null
            },
            Customer = new CustomerSnapshot
            {
                FullName = customer.FullName,
                OrganizationName = customer.OrganizationName,
                TaxCode = customer.TaxCode,
                PhoneNumber = customer.PhoneNumber,
                CitizenId = customer.CitizenId,
                CitizenIdIssuedDate = customer.CitizenIdIssuedDate,
                CitizenIdIssuedPlace = customer.CitizenIdIssuedPlace,
                DateOfBirth = customer.DateOfBirth,
                Address = customer.Address,
                Email = customer.Email
            },
            Vehicle = new VehicleSnapshot
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
                OwnerName = driver.FullName,
                OwnerCitizenId = driver.CitizenId,
                OwnerCitizenIdIssuedDate = driver.CitizenIdIssuedDate,
                OwnerCitizenIdIssuedPlace = driver.CitizenIdIssuedPlace,
                OwnerAddress = driver.Address,
                OwnerPhoneNumber = driver.PhoneNumber,
                OwnerSignatureFileUrl = driver.VehicleOwnerSignatureFileUrl,
                OwnerSignatureHash = driver.VehicleOwnerSignatureHash,
                OwnerSignedAt = driver.VehicleOwnerSignedAt,
            }
        };

    /// <summary>
    /// Tạo snapshot cho hợp đồng cũ. Các cột snapshot cũ luôn được ưu tiên để
    /// không ghi đè tên/CCCD/biển số lịch sử bằng dữ liệu danh mục hiện tại.
    /// </summary>
    public static ContractSnapshotData CaptureLegacy(Contract contract)
        => CaptureLegacy(
            contract,
            contract.CompanyProfile,
            contract.Driver,
            contract.Customer,
            contract.Vehicle);

    /// <summary>
    /// Tạo snapshot legacy bằng các entity danh mục được truyền riêng. Overload này
    /// cho phép caller dùng AsNoTracking mà KHÔNG cần gán các entity detached vào
    /// navigation của một Contract đang được DbContext khác track. Nhờ đó tránh
    /// việc EF vô tình attach hai ApplicationUser cùng Id vào một ChangeTracker.
    /// </summary>
    public static ContractSnapshotData CaptureLegacy(
        Contract contract,
        CompanyProfile? company,
        ApplicationUser? driver,
        Customer? customer,
        Vehicle? vehicle)
    {
        return new ContractSnapshotData
        {
            Version = 1,
            CapturedAtUtc = contract.CompletedAt ?? contract.UpdatedAt ?? contract.CreatedAt,
            Company = new CompanySnapshot
            {
                Name = First(contract.CompanyNameSnapshot, company?.CompanyName),
                BranchName = company?.BranchName,
                TaxCode = First(contract.CompanyTaxCodeSnapshot, company?.TaxCode),
                BusinessLicenseNumber = company?.BusinessLicenseNumber,
                Address = First(contract.CompanyAddressSnapshot, company?.Address),
                PhoneNumber = company?.PhoneNumber,
                Email = company?.Email,
                RepresentativeName = First(contract.CompanyRepresentativeSnapshot, company?.RepresentativeName),
                RepresentativePosition = First(contract.CompanyRepresentativePositionSnapshot, company?.RepresentativePosition),
                RepresentativeCitizenId = company?.RepresentativeCitizenId,
                RepresentativeCitizenIdIssuedDate = company?.RepresentativeCitizenIdIssuedDate,
                RepresentativeCitizenIdIssuedPlace = company?.RepresentativeCitizenIdIssuedPlace,
                RepresentativeSignatureFileUrl = company?.RepresentativeSignatureFileUrl,
                RepresentativeSignatureHash = company?.RepresentativeSignatureHash,
                RepresentativeSignedAt = company?.RepresentativeSignedAt
            },
            Driver = new DriverSnapshot
            {
                UserId = contract.DriverId,
                FullName = First(contract.DriverNameSnapshot, driver?.FullName),
                PhoneNumber = driver?.PhoneNumber,
                CitizenId = driver?.CitizenId,
                CitizenIdIssuedDate = driver?.CitizenIdIssuedDate,
                CitizenIdIssuedPlace = driver?.CitizenIdIssuedPlace,
                Address = driver?.Address,
                AreaCode = driver?.AreaCode,
                DriverLicenseNumber = First(contract.DriverLicenseNumberSnapshot, driver?.DriverLicenseNumber),
                DriverLicenseClass = First(contract.DriverLicenseClassSnapshot, driver?.DriverLicenseClass),
                DriverLicenseIssuedDate = driver?.DriverLicenseIssuedDate,
                DriverLicenseExpiryDate = driver?.DriverLicenseExpiryDate,
                SignatureFileUrl = null,
                SignatureHash = null,
                SignedAt = null
            },
            Customer = new CustomerSnapshot
            {
                FullName = First(contract.CustomerNameSnapshot, customer?.FullName),
                OrganizationName = customer?.OrganizationName,
                TaxCode = customer?.TaxCode,
                PhoneNumber = First(contract.CustomerPhoneSnapshot, customer?.PhoneNumber),
                CitizenId = First(contract.CustomerCitizenIdSnapshot, customer?.CitizenId),
                CitizenIdIssuedDate = customer?.CitizenIdIssuedDate,
                CitizenIdIssuedPlace = customer?.CitizenIdIssuedPlace,
                DateOfBirth = customer?.DateOfBirth,
                Address = First(contract.CustomerAddressSnapshot, customer?.Address),
                Email = customer?.Email
            },
            Vehicle = new VehicleSnapshot
            {
                PlateNumber = First(contract.VehiclePlateSnapshot, vehicle?.PlateNumber),
                VehicleCode = vehicle?.VehicleCode,
                Brand = First(contract.VehicleBrandSnapshot, vehicle?.Brand),
                Model = vehicle?.Model,
                VehicleType = vehicle?.VehicleType,
                SeatCount = vehicle?.SeatCount,
                Color = vehicle?.Color,
                ChassisNumber = vehicle?.ChassisNumber,
                EngineNumber = vehicle?.EngineNumber,
                OwnerName = First(contract.VehicleOwnerNameSnapshot, driver?.FullName),
                OwnerCitizenId = First(contract.VehicleOwnerCitizenIdSnapshot, driver?.CitizenId),
                OwnerCitizenIdIssuedDate = driver?.CitizenIdIssuedDate,
                OwnerCitizenIdIssuedPlace = driver?.CitizenIdIssuedPlace,
                OwnerAddress = driver?.Address,
                OwnerPhoneNumber = driver?.PhoneNumber,
                OwnerSignatureFileUrl = driver?.VehicleOwnerSignatureFileUrl,
                OwnerSignatureHash = driver?.VehicleOwnerSignatureHash,
                OwnerSignedAt = driver?.VehicleOwnerSignedAt,
            }
        };
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
    public DateTime? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? Email { get; set; }
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
