using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Companies;
using HTX586CONTRACT.Domain.Identity;

namespace HTX586CONTRACT.Domain.Offices;

/// <summary>Liên kết N:N giữa tài khoản Admin và Công ty/Văn phòng.</summary>
public sealed class AdminOffice : BaseEntity
{
    public string AdminUserId { get; set; } = string.Empty;
    public Guid CompanyProfileId { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public string? AssignedByUserId { get; set; }

    public ApplicationUser AdminUser { get; set; } = null!;
    public CompanyProfile CompanyProfile { get; set; } = null!;
}
