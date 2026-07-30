using HTX586CONTRACT.Domain.Enums;

namespace HTX586CONTRACT.Application.Contracts;

public sealed class ContractFilter
{
    public string? Search { get; set; }
    public ContractStatus? Status { get; set; }
    public string? DriverId { get; set; }
    public string? AdminId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public sealed class ContractPassengerDto
{
    public int SortOrder { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int? BirthYear { get; set; }
    public string? Note { get; set; }
}

public sealed class ContractSignatureDto
{
    public SignatureParty Party { get; set; }
    public string SignerName { get; set; } = string.Empty;
    public string SignatureFileUrl { get; set; } = string.Empty;
    public DateTime ServerSignedAt { get; set; }
}

public sealed class ContractDetailDto
{
    public Guid Id { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public ContractStatus Status { get; set; }
    public bool IsFinalized { get; set; }

    public string AdminId { get; set; } = string.Empty;
    public string DriverId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyTaxCode { get; set; }
    public string? CompanyAddress { get; set; }
    public string? CompanyPhone { get; set; }
    public string? CompanyRepresentativeName { get; set; }
    public string? CompanyRepresentativePosition { get; set; }
    public string? CompanyRepresentativeSignatureFileUrl { get; set; }
    public DateTime? CompanyRepresentativeSignedAt { get; set; }

    public string DriverName { get; set; } = string.Empty;
    public string? DriverPhone { get; set; }
    public string? DriverCitizenId { get; set; }
    public string? DriverLicenseNumber { get; set; }
    public string? DriverLicenseClass { get; set; }
    public string? DriverSignatureFileUrl { get; set; }
    public DateTime? DriverSignedAt { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerCitizenId { get; set; }
    public DateTime? CustomerCitizenIdIssuedDate { get; set; }
    public string? CustomerCitizenIdIssuedPlace { get; set; }
    public string? CustomerAddress { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerOrganizationName { get; set; }
    public string? CustomerTaxCode { get; set; }

    public string AreaCode { get; set; } = string.Empty;
    public string? VehiclePlate { get; set; }
    public string? VehicleCode { get; set; }
    public string? VehicleBrand { get; set; }
    public string? VehicleModel { get; set; }
    public string? VehicleType { get; set; }
    public int? SeatCount { get; set; }
    public string? VehicleColor { get; set; }
    public string? ChassisNumber { get; set; }
    public string? EngineNumber { get; set; }
    public int? ActualPassengerCount { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerCitizenId { get; set; }
    public DateTime? OwnerCitizenIdIssuedDate { get; set; }
    public string? OwnerCitizenIdIssuedPlace { get; set; }
    public string? OwnerAddress { get; set; }
    public string? OwnerPhoneNumber { get; set; }
    public string? VehicleOwnerSignatureFileUrl { get; set; }
    public DateTime? VehicleOwnerSignedAt { get; set; }

    public string? SecondDriverName { get; set; }
    public string? SecondDriverLicenseClass { get; set; }
    public string? PickupLocation { get; set; }
    public string? DropoffLocation { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? RouteDescription { get; set; }
    public decimal? TotalKilometers { get; set; }
    public decimal? ContractValue { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentTime { get; set; }
    public string? Note { get; set; }
    public string? PdfFileUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = "Hệ thống";
    public List<ContractPassengerDto> Passengers { get; set; } = [];
    public List<ContractSignatureDto> Signatures { get; set; } = [];
}

public sealed class SaveContractRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerCitizenId { get; set; }
    public DateTime? CustomerCitizenIdIssuedDate { get; set; }
    public string? CustomerCitizenIdIssuedPlace { get; set; }
    public string? CustomerAddress { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerOrganizationName { get; set; }
    public string? CustomerTaxCode { get; set; }

    public string AreaCode { get; set; } = string.Empty;
    public string? VehiclePlate { get; set; }
    public string? VehicleCode { get; set; }
    public string? VehicleBrand { get; set; }
    public string? VehicleModel { get; set; }
    public string? VehicleType { get; set; }
    public int? SeatCount { get; set; }
    public string? VehicleColor { get; set; }
    public string? ChassisNumber { get; set; }
    public string? EngineNumber { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerCitizenId { get; set; }
    public DateTime? OwnerCitizenIdIssuedDate { get; set; }
    public string? OwnerCitizenIdIssuedPlace { get; set; }
    public string? OwnerAddress { get; set; }
    public string? OwnerPhoneNumber { get; set; }

    public string? SecondDriverName { get; set; }
    public string? SecondDriverLicenseClass { get; set; }
    public string? PickupLocation { get; set; }
    public string? DropoffLocation { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? RouteDescription { get; set; }
    public decimal? TotalKilometers { get; set; }
    public decimal? ContractValue { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentTime { get; set; }
    public string? Note { get; set; }
    public List<ContractPassengerDto> Passengers { get; set; } = [];
}

public sealed record SaveContractResult(bool Succeeded, Guid? Id, string Message);
