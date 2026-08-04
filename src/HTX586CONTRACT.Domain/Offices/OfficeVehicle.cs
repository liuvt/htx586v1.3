using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Companies;
using HTX586CONTRACT.Domain.Vehicles;

namespace HTX586CONTRACT.Domain.Offices;

/// <summary>Liên kết N:N giữa xe và Công ty/Văn phòng nơi xe được phép hoạt động.</summary>
public sealed class OfficeVehicle : BaseEntity
{
    public Guid VehicleId { get; set; }
    public Guid CompanyProfileId { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime AssignedFrom { get; set; } = DateTime.UtcNow;
    public DateTime? AssignedTo { get; set; }

    public Vehicle Vehicle { get; set; } = null!;
    public CompanyProfile CompanyProfile { get; set; } = null!;
}
