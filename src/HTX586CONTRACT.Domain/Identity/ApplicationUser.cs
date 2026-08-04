using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Customers;
using HTX586CONTRACT.Domain.Notifications;
using HTX586CONTRACT.Domain.Offices;
using HTX586CONTRACT.Domain.Vehicles;
using Microsoft.AspNetCore.Identity;

namespace HTX586CONTRACT.Domain.Identity;

/// <summary>
/// Tài khoản dùng chung cho ba vai trò hệ thống: Owner, Admin và VehicleOwner.
/// Admin được phân quyền nhiều Công ty/Văn phòng qua AdminOffice; VehicleOwner
/// sở hữu nhiều xe và phạm vi văn phòng được xác định qua OfficeVehicle.
/// </summary>
public class ApplicationUser : IdentityUser, ISoftDeletable
{
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }


    public string? CitizenId { get; set; }
    public DateTime? CitizenIdIssuedDate { get; set; }
    public string? CitizenIdIssuedPlace { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? AreaCode { get; set; }
    public string? AvatarUrl { get; set; }
    public string? CitizenIdFrontUrl { get; set; }
    public string? CitizenIdBackUrl { get; set; }
    public string? DriverLicenseNumber { get; set; }
    public string? DriverLicenseClass { get; set; }
    public DateTime? DriverLicenseIssuedDate { get; set; }
    public DateTime? DriverLicenseExpiryDate { get; set; }
    public string? DriverLicenseFrontUrl { get; set; }
    public string? DriverLicenseBackUrl { get; set; }

    // Chữ ký cấp tài khoản phục vụ hồ sơ đăng ký. Khi lập hợp đồng,
    // chữ ký theo từng Vehicle là nguồn dữ liệu ưu tiên.
    public string? DriverSignatureFileUrl { get; set; }
    public string? DriverSignatureHash { get; set; }
    public DateTime? DriverSignedAt { get; set; }
    public bool DriverSignatureIsActive { get; set; }
    public DateTime? DriverSignatureInactiveAt { get; set; }

    // Trạng thái đăng ký VehicleOwner: Approved, Pending hoặc Rejected.
    public string RegistrationStatus { get; set; } = "Approved";
    public DateTime? RegistrationRequestedAt { get; set; }
    public DateTime? RegistrationViewedAt { get; set; }
    public string? RegistrationViewedByUserId { get; set; }
    public DateTime? RegistrationReviewedAt { get; set; }
    public string? RegistrationReviewedByUserId { get; set; }
    public string? RegistrationReviewNote { get; set; }

    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    public ICollection<Contract> Contracts { get; set; } = [];
    public ICollection<AdminOffice> AdminOffices { get; set; } = [];
    public ICollection<Vehicle> OwnedVehicles { get; set; } = [];
    public ICollection<Customer> CreatedCustomers { get; set; } = [];
    public ICollection<DriverNotification> DriverNotifications { get; set; } = [];
}
