using HTX586CONTRACT.Domain.Enums;

namespace HTX586CONTRACT.Application.Contracts;

public sealed class ContractFilter
{
    public string? Search { get; set; }
    public ContractStatus? Status { get; set; }
    public ContractBusinessType? BusinessType { get; set; }
    public string? DriverId { get; set; }
    public Guid? CompanyProfileId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public sealed class ContractPassengerDto
{
    public Guid? Id { get; set; }
    public int SortOrder { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int? BirthYear { get; set; }
    public string? Note { get; set; }
}

public sealed class ContractSignatureDto
{
    public Guid Id { get; set; }
    public SignatureParty Party { get; set; }
    public string SignerName { get; set; } = string.Empty;
    public string SignatureFileUrl { get; set; } = string.Empty;
    public DateTime ServerSignedAt { get; set; }
}

public sealed class ContractDetailDto
{
    public Guid Id { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public ContractBusinessType BusinessType { get; set; }
    public Guid ContractTypeId { get; set; }
    public ContractStatus Status { get; set; }
    public bool IsSelfCreated { get; set; }
    public bool IsLocked { get; set; }

    public Guid CompanyProfileId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyRepresentativeName { get; set; }
    public string? CompanyRepresentativeSignatureFileUrl { get; set; }
    public DateTime? CompanyRepresentativeSignedAt { get; set; }

    public string DriverId { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string? DriverLicenseClass { get; set; }
    public string? DriverSignatureFileUrl { get; set; }
    public DateTime? DriverSignedAt { get; set; }

    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerRepresentativeName { get; set; }
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerCitizenId { get; set; }
    public DateTime? CustomerCitizenIdIssuedDate { get; set; }
    public string? CustomerTaxCode { get; set; }
    public bool CustomerIsCompany { get; set; }
    public string? CustomerAddress { get; set; }
    public bool CustomerTravelsWithGroup { get; set; }

    public string AreaCode { get; set; } = string.Empty;
    public Guid? VehicleId { get; set; }
    public string? VehiclePlate { get; set; }
    public string? VehicleCode { get; set; }
    public string? VehicleBrand { get; set; }
    public int? SeatCount { get; set; }
    public int? ActualPassengerCount { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerCitizenId { get; set; }
    public DateTime? OwnerCitizenIdIssuedDate { get; set; }
    public string? VehicleOwnerSignatureFileUrl { get; set; }
    public DateTime? VehicleOwnerSignedAt { get; set; }

    public string? OperatingDriverName { get; set; }
    public string? OperatingDriverPhoneNumber { get; set; }
    public string? OperatingDriverLicenseNumber { get; set; }
    public string? OperatingDriverLicenseClass { get; set; }
    public string? SecondDriverName { get; set; }
    public string? SecondDriverLicenseClass { get; set; }

    public string? CargoName { get; set; }
    public decimal? CargoWeight { get; set; }
    public string? CargoUnit { get; set; }
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
    public string? AssignedByUserId { get; set; }
    public string? AssignedByName { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? LockedAt { get; set; }

    public List<ContractPassengerDto> Passengers { get; set; } = [];
    public List<ContractSignatureDto> Signatures { get; set; } = [];
}

public sealed class SaveContractRequest
{
    public Guid? Id { get; set; }
    public string? ContractNumber { get; set; }
    public ContractBusinessType BusinessType { get; set; } = ContractBusinessType.Passenger;
    public Guid? ContractTypeId { get; set; }
    public Guid? CompanyProfileId { get; set; }
    public string DriverId { get; set; } = string.Empty;

    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerRepresentativeName { get; set; }
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerCitizenId { get; set; }
    public DateTime? CustomerCitizenIdIssuedDate { get; set; }
    public string? CustomerAddress { get; set; }
    public bool CustomerIsCompany { get; set; }
    public string? CustomerTaxCode { get; set; }
    public bool CustomerTravelsWithGroup { get; set; }

    public string AreaCode { get; set; } = string.Empty;
    public Guid? VehicleId { get; set; }
    public string? VehiclePlate { get; set; }
    public string? VehicleCode { get; set; }
    public string? VehicleBrand { get; set; }
    public int? SeatCount { get; set; }
    public int? ActualPassengerCount { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerCitizenId { get; set; }
    public DateTime? OwnerCitizenIdIssuedDate { get; set; }

    public string? OperatingDriverName { get; set; }
    public string? OperatingDriverPhoneNumber { get; set; }
    public string? OperatingDriverLicenseNumber { get; set; }
    public string? OperatingDriverLicenseClass { get; set; }
    public string? SecondDriverName { get; set; }
    public string? SecondDriverLicenseClass { get; set; }

    public string? CargoName { get; set; }
    public decimal? CargoWeight { get; set; }
    public string? CargoUnit { get; set; }
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
    public ContractStatus Status { get; set; } = ContractStatus.Created;
    public List<ContractPassengerDto> Passengers { get; set; } = [];
}

public sealed record SaveContractResult(bool Succeeded, Guid? Id, string Message);
