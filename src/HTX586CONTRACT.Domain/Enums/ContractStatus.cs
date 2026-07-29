namespace HTX586CONTRACT.Domain.Enums;

// Trạng thái hợp đồng
public enum ContractStatus
{
    // Trạng thái hợp đồng đang được tạo, chưa có chữ ký của khách hàng.
    Draft = 0,
    // Trạng thái hợp đồng đang chờ chữ ký của khách hàng.
    WaitingCustomerSignature = 1,
    // Trạng thái hợp đồng đã được khách hàng ký, đang chờ xác nhận của tài xế.
    CustomerSigned = 2,
    // Trạng thái hợp đồng đang chờ xác nhận của tài xế.
    WaitingDriverConfirmation = 3,
    // Trạng thái hợp đồng đã được tài xế xác nhận, hợp đồng có hiệu lực.
    Completed = 4,
    // Trạng thái hợp đồng đã bị hủy.
    Cancelled = 5,
    // Trạng thái hợp đồng đã hết hạn.
    Expired = 6,
    // Trạng thái hợp đồng đã bị vô hiệu hóa.
    Invalidated = 7
}
