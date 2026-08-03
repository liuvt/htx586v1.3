namespace HTX586CONTRACT.Domain.Enums;

// Loại tệp đính kèm
public enum AttachmentType
{
    // Mặt trước của CMND/CCCD/Hộ chiếu của khách hàng.
    CustomerCitizenIdFront = 1,
    // Mặt sau của CMND/CCCD/Hộ chiếu của khách hàng.
    CustomerCitizenIdBack = 2,
    // Hình ảnh hợp đồng (nếu có).
    ContractImage = 3,
    // Hình ảnh biên lai (nếu có).
    Receipt = 4,
    // Hình ảnh hóa đơn (nếu có).
    Other = 99
}
