using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Companies;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Identity;

namespace HTX586CONTRACT.Domain.Vehicles;

/// <summary>
/// Danh mục xe của một Admin/công ty. Hợp đồng mới không phụ thuộc danh mục này;
/// tài xế nhập snapshot xe và chủ xe trực tiếp trên từng hợp đồng.
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

    // Luồng mới: xe thuộc trực tiếp Admin. Có thể chuyển công ty bằng cách đổi AdminId.
    public string? AdminId { get; set; }
    public ApplicationUser? AdminAccount { get; set; }

    // Quan hệ cũ giữ để tương thích dữ liệu đã có.
    public Guid? CompanyProfileId { get; set; }
    public CompanyProfile? CompanyProfile { get; set; }

    // Không còn ràng buộc xe với tài xế trong luồng tạo hợp đồng mới.
    public string? AssignedDriverId { get; set; }
    public ApplicationUser? AssignedDriver { get; set; }

    // Chữ ký danh mục cũ; hợp đồng mới dùng chữ ký chủ xe ký tay theo từng hợp đồng.
    public string? OwnerSignatureFileUrl { get; set; }
    public string? OwnerSignatureHash { get; set; }
    public DateTime? OwnerSignedAt { get; set; }

    public bool IsActive { get; set; } = true;
    public ICollection<Contract> Contracts { get; set; } = [];
}
