using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Identity;

namespace HTX586CONTRACT.Domain.Vehicles;

/// <summary>
/// Xe thuộc trực tiếp một Admin/công ty. Danh mục xe độc lập với dữ liệu snapshot
/// mà tài xế nhập tay trên từng hợp đồng.
/// </summary>
public sealed class Vehicle : BaseEntity
{
    public string PlateNumber { get; set; } = string.Empty;
    public string? VehicleCode { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? VehicleType { get; set; }
    public int? SeatCount { get; set; }
    public string? Color { get; set; }
    public string? ChassisNumber { get; set; }
    public string? EngineNumber { get; set; }

    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerCitizenId { get; set; }
    public DateTime? OwnerCitizenIdIssuedDate { get; set; }
    public string? OwnerCitizenIdIssuedPlace { get; set; }
    public string? OwnerAddress { get; set; }
    public string? OwnerPhoneNumber { get; set; }

    public string AdminId { get; set; } = string.Empty;
    public ApplicationUser AdminAccount { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}
