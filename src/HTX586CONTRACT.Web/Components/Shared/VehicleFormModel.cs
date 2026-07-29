using HTX586CONTRACT.Domain.Vehicles;

namespace HTX586CONTRACT.Web.Components.Shared;

public sealed class VehicleFormModel
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
    public bool IsActive { get; set; } = true;

    public bool IsValid(out string error)
    {
        if (string.IsNullOrWhiteSpace(PlateNumber)) { error = "Vui lòng nhập biển số xe."; return false; }
        if (string.IsNullOrWhiteSpace(OwnerName)) { error = "Vui lòng nhập họ tên chủ sở hữu."; return false; }
        if (SeatCount is <= 0) { error = "Số chỗ phải lớn hơn 0."; return false; }
        error = string.Empty;
        return true;
    }

    public Vehicle ToEntity()
    {
        var entity = new Vehicle();
        ApplyTo(entity);
        return entity;
    }

    public void ApplyTo(Vehicle entity)
    {
        entity.PlateNumber = PlateNumber.Trim().ToUpperInvariant();
        entity.VehicleCode = N(VehicleCode);
        entity.Brand = N(Brand);
        entity.Model = N(Model);
        entity.VehicleType = N(VehicleType);
        entity.SeatCount = SeatCount;
        entity.Color = N(Color);
        entity.ChassisNumber = N(ChassisNumber);
        entity.EngineNumber = N(EngineNumber);
        entity.OwnerName = OwnerName.Trim();
        entity.OwnerCitizenId = N(OwnerCitizenId);
        entity.OwnerCitizenIdIssuedDate = OwnerCitizenIdIssuedDate;
        entity.OwnerCitizenIdIssuedPlace = N(OwnerCitizenIdIssuedPlace);
        entity.OwnerAddress = N(OwnerAddress);
        entity.OwnerPhoneNumber = N(OwnerPhoneNumber);
        entity.IsActive = IsActive;
    }

    public static VehicleFormModel FromEntity(Vehicle x) => new()
    {
        PlateNumber = x.PlateNumber,
        VehicleCode = x.VehicleCode,
        Brand = x.Brand,
        Model = x.Model,
        VehicleType = x.VehicleType,
        SeatCount = x.SeatCount,
        Color = x.Color,
        ChassisNumber = x.ChassisNumber,
        EngineNumber = x.EngineNumber,
        OwnerName = x.OwnerName,
        OwnerCitizenId = x.OwnerCitizenId,
        OwnerCitizenIdIssuedDate = x.OwnerCitizenIdIssuedDate,
        OwnerCitizenIdIssuedPlace = x.OwnerCitizenIdIssuedPlace,
        OwnerAddress = x.OwnerAddress,
        OwnerPhoneNumber = x.OwnerPhoneNumber,
        IsActive = x.IsActive
    };

    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
