namespace HTX586CONTRACT.Application.Common;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Provider hiện tại là LocalFileSystem. Thuộc tính này được giữ để sau này mở rộng sang NAS/S3/MinIO/Azure Blob.
    /// </summary>
    public string Provider { get; set; } = "LocalFileSystem";

    /// <summary>
    /// Thư mục chứa file upload.
    /// Nếu là đường dẫn tương đối thì được tính từ DataStorage:RootPath, không tính từ source/publish.
    /// Có thể dùng đường dẫn tuyệt đối khi muốn lưu trên ổ đĩa hoặc volume riêng.
    /// </summary>
    public string UploadRootPath { get; set; } = "upload";

    /// <summary>
    /// URL public dùng để mở ảnh chữ ký/PDF đã lưu. Giữ mặc định /uploads để tương thích dữ liệu cũ trong database.
    /// </summary>
    public string PublicRequestPath { get; set; } = "/uploads";

    /// <summary>
    /// Bật phục vụ file upload bằng static file middleware.
    /// </summary>
    public bool ServeUploadsAsStaticFiles { get; set; } = true;
}
