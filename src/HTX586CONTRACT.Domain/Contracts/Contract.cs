using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Companies;
using HTX586CONTRACT.Domain.Customers;
using HTX586CONTRACT.Domain.Enums;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Domain.Signatures;
using HTX586CONTRACT.Domain.Vehicles;

namespace HTX586CONTRACT.Domain.Contracts;

public class Contract : BaseEntity
{
    public string ContractNumber { get; set; } = string.Empty;
    public ContractBusinessType BusinessType { get; set; }
    public Guid ContractTypeId { get; set; }
    public Guid ContractTemplateId { get; set; }

    public Guid CompanyProfileId { get; set; }

    // Tên property cũ được giữ tương thích database. Giá trị là tài khoản VehicleOwner nhận/tạo HĐ.
    public string DriverId { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid? VehicleId { get; set; }

    public ContractStatus Status { get; set; }
    public bool IsSelfCreated { get; set; }
    public string? AssignedByUserId { get; set; }
    public string? AssignedByNameSnapshot { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? LockedAt { get; set; }

    public string AreaCode { get; set; } = string.Empty;
    public string? CargoName { get; set; }
    public decimal? CargoWeight { get; set; }
    public string? CargoUnit { get; set; }
    public int? ActualPassengerCount { get; set; }
    public bool CustomerTravelsWithGroup { get; set; }
    public int? CustomerTravelBirthYear { get; set; }
    public string? CustomerTravelNote { get; set; }

    // Người trực tiếp điều khiển xe được nhập tay trên HĐ, không phụ thuộc hồ sơ tài khoản.
    public string? OperatingDriverName { get; set; }
    public string? OperatingDriverPhoneNumber { get; set; }
    public string? OperatingDriverLicenseNumber { get; set; }
    public string? OperatingDriverLicenseClass { get; set; }

    // Giữ tương thích mẫu/PDF cũ.
    public string? SecondDriverName { get; set; }
    public string? SecondDriverLicenseClass { get; set; }

    public string? RouteDescription { get; set; }
    public decimal? TotalKilometers { get; set; }
    public string? PickupLocation { get; set; }
    public string? DropoffLocation { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal? ContractValue { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentTime { get; set; }
    public string? Note { get; set; }

    // Snapshot bất biến.
    public string CompanyNameSnapshot { get; set; } = string.Empty;
    public string CompanyTaxCodeSnapshot { get; set; } = string.Empty;
    public string CompanyAddressSnapshot { get; set; } = string.Empty;
    public string CompanyRepresentativeSnapshot { get; set; } = string.Empty;
    public string? CompanyRepresentativePositionSnapshot { get; set; }

    public string DriverNameSnapshot { get; set; } = string.Empty;
    public string? DriverLicenseNumberSnapshot { get; set; }
    public string? DriverLicenseClassSnapshot { get; set; }

    public string CustomerNameSnapshot { get; set; } = string.Empty;
    public string CustomerPhoneSnapshot { get; set; } = string.Empty;
    public string? CustomerCitizenIdSnapshot { get; set; }
    public string? CustomerAddressSnapshot { get; set; }

    public string? VehiclePlateSnapshot { get; set; }
    public string? VehicleBrandSnapshot { get; set; }
    public string? VehicleOwnerNameSnapshot { get; set; }
    public string? VehicleOwnerCitizenIdSnapshot { get; set; }

    public string ContractContentSnapshot { get; set; } = string.Empty;
    public string ContractDataJson { get; set; } = "{}";
    public string? ContractHash { get; set; }
    public string? PdfFileUrl { get; set; }
    public string? PdfSha256 { get; set; }
    public DateTime? PdfGeneratedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public bool IsLocked => Status is ContractStatus.Completed or ContractStatus.Cancelled;

    public CompanyProfile CompanyProfile { get; set; } = null!;
    public ContractType ContractType { get; set; } = null!;
    public ContractTemplate ContractTemplate { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public Vehicle? Vehicle { get; set; }
    public ApplicationUser Driver { get; set; } = null!;
    public ICollection<ContractSignature> Signatures { get; set; } = [];
    public ICollection<ContractPassenger> Passengers { get; set; } = [];
    public ICollection<ContractAttachment> Attachments { get; set; } = [];
    public ICollection<ContractAuditLog> AuditLogs { get; set; } = [];
}
