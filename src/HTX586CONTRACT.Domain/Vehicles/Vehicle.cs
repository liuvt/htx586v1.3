using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Domain.Offices;

namespace HTX586CONTRACT.Domain.Vehicles;

/// <summary>
/// Xe thuộc đúng một Công ty/Văn phòng qua OfficeVehicle. Chủ xe là tùy chọn.
/// Một VehicleOwner có thể có nhiều xe; một xe chỉ được gán cho tối đa một tài khoản Chủ xe.
/// </summary>
public class Vehicle : BaseEntity
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


    // Tài khoản role VehicleOwner sở hữu/được gán xe này.
    // Một VehicleOwner có thể liên kết nhiều Vehicle.
    public string? AssignedDriverId { get; set; }
    public ApplicationUser? AssignedDriver { get; set; }

    // Chân ký Chủ xe KHÔNG lưu trên từng xe. Nguồn duy nhất là
    // AssignedDriver.VehicleOwnerSignature* (tài khoản role VehicleOwner).
    // Khi lập hợp đồng, chữ ký được chụp vào ContractDataJson để khóa theo HĐ.

    public bool IsActive { get; set; } = true;
    public ICollection<Contract> Contracts { get; set; } = [];
    public ICollection<OfficeVehicle> OfficeVehicles { get; set; } = [];
}
