using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Contracts;
using Microsoft.AspNetCore.Identity;

namespace HTX586CONTRACT.Domain.Identity;

public class ApplicationUser : IdentityUser, ISoftDeletable
{
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }

    // Mỗi Driver thuộc trực tiếp một Admin. Mỗi Admin đại diện cho một công ty.
    public string? AdminId { get; set; }
    public ApplicationUser? AdminAccount { get; set; }
    public ICollection<ApplicationUser> ManagedDrivers { get; set; } = [];

    // Hồ sơ công ty lưu trực tiếp trên tài khoản Admin.
    public string? CompanyName { get; set; }
    public string? CompanyBranchName { get; set; }
    public string? CompanyTaxCode { get; set; }
    public string? CompanyBusinessLicenseNumber { get; set; }
    public string? CompanyAddress { get; set; }
    public string? CompanyPhoneNumber { get; set; }
    public string? CompanyEmail { get; set; }
    public string? CompanyRepresentativeName { get; set; }
    public string? CompanyRepresentativePosition { get; set; }
    public string? CompanyRepresentativeCitizenId { get; set; }
    public DateTime? CompanyRepresentativeCitizenIdIssuedDate { get; set; }
    public string? CompanyRepresentativeCitizenIdIssuedPlace { get; set; }
    public string? CompanySignatureFileUrl { get; set; }
    public string? CompanySignatureHash { get; set; }
    public DateTime? CompanySignedAt { get; set; }

    // Hồ sơ cá nhân tài xế.
    public string? CitizenId { get; set; }
    public DateTime? CitizenIdIssuedDate { get; set; }
    public string? CitizenIdIssuedPlace { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? AreaCode { get; set; }
    public string? DriverLicenseNumber { get; set; }
    public string? DriverLicenseClass { get; set; }
    public DateTime? DriverLicenseIssuedDate { get; set; }
    public DateTime? DriverLicenseExpiryDate { get; set; }
    public string? DriverSignatureFileUrl { get; set; }
    public string? DriverSignatureHash { get; set; }
    public DateTime? DriverSignedAt { get; set; }

    // Quy trình đăng ký Driver.
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
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    public ICollection<Contract> Contracts { get; set; } = [];
    public ICollection<Contract> AdminContracts { get; set; } = [];
}
