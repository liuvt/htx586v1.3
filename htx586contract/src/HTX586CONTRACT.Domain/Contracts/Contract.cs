using HTX586CONTRACT.Domain.Enums;
using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Identity;

namespace HTX586CONTRACT.Domain.Contracts;

/// <summary>
/// Một dòng hợp đồng chứa toàn bộ snapshot công ty, tài xế, xe/chủ xe,
/// khách hàng, hành khách và bốn chữ ký trong ContractDataJson.
/// </summary>
public sealed class Contract : BaseEntity
{
    public string ContractNumber { get; set; } = string.Empty;
    public ContractStatus Status { get; set; } = ContractStatus.WaitingCustomerSignature;

    public string AdminId { get; set; } = string.Empty;
    public string DriverId { get; set; } = string.Empty;

    // Các cột phục vụ tìm kiếm/danh sách. Dữ liệu đầy đủ nằm trong ContractDataJson.
    public string CompanyNameSnapshot { get; set; } = string.Empty;
    public string DriverNameSnapshot { get; set; } = string.Empty;
    public string CustomerNameSnapshot { get; set; } = string.Empty;
    public string CustomerPhoneSnapshot { get; set; } = string.Empty;
    public string? VehiclePlateSnapshot { get; set; }
    public string? VehicleOwnerNameSnapshot { get; set; }

    public string AreaCode { get; set; } = string.Empty;
    public int? ActualPassengerCount { get; set; }
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

    public string ContractDataJson { get; set; } = "{}";
    public string? ContractHash { get; set; }
    public string? PdfFileUrl { get; set; }
    public string? PdfSha256 { get; set; }
    public DateTime? PdfGeneratedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ApplicationUser AdminAccount { get; set; } = null!;
    public ApplicationUser Driver { get; set; } = null!;
}
