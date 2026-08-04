using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Domain.Offices;

namespace HTX586CONTRACT.Domain.Vehicles;

/// <summary>
/// Xe, chủ sở hữu pháp lý, đơn vị quản lý và tài khoản VehicleOwner được cấp xe.
/// Một VehicleOwner có thể có nhiều xe; một xe chỉ được gán cho tối đa một tài khoản.
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

    // Chữ ký cố định của chủ sở hữu pháp lý của xe.
    public string? OwnerSignatureFileUrl { get; set; }
    public string? OwnerSignatureHash { get; set; }
    public DateTime? OwnerSignedAt { get; set; }

    // Chữ ký tài xế/VehicleOwner theo từng xe. Chỉ tài khoản được gán xe được ký,
    // và sau khi ký thành công không được tự thay đổi lần hai.
    public string? AccountDriverSignatureFileUrl { get; set; }
    public string? AccountDriverSignatureHash { get; set; }
    public DateTime? AccountDriverSignedAt { get; set; }
    public string? AccountDriverSignedByUserId { get; set; }

    public bool IsActive { get; set; } = true;
    public ICollection<Contract> Contracts { get; set; } = [];
    public ICollection<OfficeVehicle> OfficeVehicles { get; set; } = [];
}
