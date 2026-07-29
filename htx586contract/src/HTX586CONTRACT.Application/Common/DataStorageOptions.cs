namespace HTX586CONTRACT.Application.Common;

/// <summary>
/// Cấu hình thư mục dữ liệu runtime của ứng dụng.
/// Thư mục này phải nằm ngoài source và ngoài thư mục publish để không bị mất khi cập nhật phiên bản.
/// </summary>
public sealed class DataStorageOptions
{
    public const string SectionName = "DataStorage";

    /// <summary>
    /// Thư mục gốc htx586contract_data chứa upload, chữ ký, PDF và Data Protection keys.
    /// Hỗ trợ đường dẫn tuyệt đối hoặc tương đối theo ContentRootPath.
    /// </summary>
    public string RootPath { get; set; } = "../../../htx586contract_data";
}
