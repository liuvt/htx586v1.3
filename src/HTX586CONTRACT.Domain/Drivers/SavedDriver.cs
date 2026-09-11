using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Identity;

namespace HTX586CONTRACT.Domain.Drivers;

/// <summary>
/// Danh mục người lái do từng tài khoản tạo để gợi ý lại khi lập hợp đồng.
/// Số GPLX là khóa chính nghiệp vụ và khóa chính database.
/// </summary>
public class SavedDriver : ISoftDeletable
{
    public string DriverLicenseNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DriverLicenseClass { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ApplicationUser CreatedByUser { get; set; } = null!;
}
