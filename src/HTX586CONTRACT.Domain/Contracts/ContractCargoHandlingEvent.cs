using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Enums;

namespace HTX586CONTRACT.Domain.Contracts;

public sealed class ContractCargoHandlingEvent : BaseEntity
{
    public Guid ContractId { get; set; }
    public CargoHandlingType Type { get; set; }
    public int SortOrder { get; set; }
    public string? Location { get; set; }
    public string? CargoWeight { get; set; }
    public DateTime? EventTime { get; set; }
    public string? Confirmation { get; set; }
    public Contract Contract { get; set; } = null!;
}
