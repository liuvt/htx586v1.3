namespace HTX586CONTRACT.Domain.Enums;

/// <summary>
/// Vòng đời hợp đồng. Hai trạng thái Completed/Cancelled là trạng thái khóa vĩnh viễn.
/// Các tên cũ được giữ dưới dạng alias để database và code cũ nâng cấp an toàn.
/// </summary>
public enum ContractStatus
{
    Created = 0,
    Draft = Created,

    Assigned = 1,
    WaitingCustomerSignature = Assigned,

    CustomerSigned = 2,

    Received = 3,
    WaitingDriverConfirmation = Received,

    Completed = 4,
    Cancelled = 5,
    Expired = 6,
    Invalidated = 7
}
