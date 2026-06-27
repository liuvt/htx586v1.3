using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Identity;

namespace HTX586CONTRACT.Domain.Companies;

/// <summary>
/// Danh mục đơn vị/văn phòng đại diện. CompanyProfile tồn tại độc lập,
/// không thuộc một tài khoản và không sở hữu xe.
/// </summary>
public class CompanyProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CompanyName { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public string TaxCode { get; set; } = string.Empty;
    public string? BusinessLicenseNumber { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string RepresentativeName { get; set; } = string.Empty;
    public string? RepresentativePosition { get; set; }
    public string RepresentativeCitizenId { get; set; } = string.Empty;
    public DateTime? RepresentativeCitizenIdIssuedDate { get; set; }
    public string? RepresentativeCitizenIdIssuedPlace { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Một đơn vị đại diện có thể được gán cho nhiều tài xế.
    public ICollection<ApplicationUser> Drivers { get; set; } = [];

    // Hợp đồng lưu CompanyProfileId để xác định đơn vị đại diện tại thời điểm lập.
    public ICollection<Contract> Contracts { get; set; } = [];
}
