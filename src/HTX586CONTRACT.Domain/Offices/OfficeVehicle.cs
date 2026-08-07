using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Companies;
using HTX586CONTRACT.Domain.Vehicles;

namespace HTX586CONTRACT.Domain.Offices;

/// <summary>Liên kết 1:1 theo phía Vehicle: mỗi xe chỉ thuộc đúng một Công ty/Văn phòng.</summary>
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
