using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Companies;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Customers;
using HTX586CONTRACT.Domain.Notifications;
using Microsoft.AspNetCore.Identity;

namespace HTX586CONTRACT.Domain.Identity;

/// <summary>
/// Tài khoản dùng chung cho ba vai trò hệ thống: Owner, Admin và VehicleOwner.
/// Chỉ Admin được gán trực tiếp vào một Công ty/Văn phòng. VehicleOwner không gán
/// đơn vị vì một tài khoản có thể sở hữu/khai thác nhiều xe thuộc nhiều đơn vị khác nhau.
/// </summary>
public class ApplicationUser : IdentityUser, ISoftDeletable
{
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }

    // Owner và VehicleOwner luôn để null. Chỉ Admin được gán CompanyProfileId.
    public Guid? CompanyProfileId { get; set; }
    public CompanyProfile? CompanyProfile { get; set; }

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

    // Các cột chữ ký cũ được giữ để tương thích dữ liệu phiên bản trước.
    // Phiên bản mới lưu chữ ký tài xế theo từng Vehicle.
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
    public ICollection<Customer> CreatedCustomers { get; set; } = [];
    public ICollection<DriverNotification> DriverNotifications { get; set; } = [];
}
