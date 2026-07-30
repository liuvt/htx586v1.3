using System.Security.Cryptography;
using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Application.Common;

namespace HTX586CONTRACT.Web.Services;

/// <summary>
/// Lưu file upload trong thư mục htx586contract_data/upload nằm cùng cấp source.
/// </summary>
public sealed class LocalUploadFileStorage(
    IWebHostEnvironment environment) : IUploadFileStorage
{
    private const int MaxSignatureBytes = 2 * 1024 * 1024;

    private string UploadRootPath => StoragePathResolver.ResolveUploadRootPath(
        environment.ContentRootPath);

    private const string PublicRequestPath = StoragePathResolver.PublicUploadPath;

    public async Task<StoredUploadFile> SaveImageDataUrlAsync(
        IReadOnlyList<string> folderSegments,
        string prefix,
        string dataUrl,
        CancellationToken ct = default)
    {
        if (folderSegments.Count == 0)
            throw new InvalidOperationException("Thiếu thư mục lưu chữ ký.");

        if (string.IsNullOrWhiteSpace(prefix))
            prefix = "signature";

        var decoded = DecodeDataUrl(dataUrl);
        var bytes = decoded.Bytes;
        if (bytes.Length == 0 || bytes.Length > MaxSignatureBytes)
            throw new InvalidOperationException("Dung lượng chữ ký không hợp lệ hoặc vượt quá 2 MB.");

        var safePrefix = SafeSegment(prefix);
        var safeFolderSegments = folderSegments.Select(SafeSegment).ToArray();
        var fileName = $"{safePrefix}-{Guid.NewGuid():N}.{decoded.Extension}";
        var physicalDirectory = GetPhysicalDirectory(safeFolderSegments);

        Directory.CreateDirectory(physicalDirectory);

        var physicalPath = Path.Combine(physicalDirectory, fileName);
        var tempPath = Path.Combine(physicalDirectory, $".{fileName}.uploading");

        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes, ct);
            File.Move(tempPath, physicalPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }

        var relativeUrl = BuildRelativeUrl(safeFolderSegments, fileName);
        return new StoredUploadFile(
            relativeUrl,
            Convert.ToHexString(SHA256.HashData(bytes)),
            DateTime.UtcNow);
    }

    public string GetPhysicalDirectory(IReadOnlyList<string> folderSegments)
    {
        if (folderSegments.Count == 0)
            return UploadRootPath;

        var safeSegments = folderSegments.Select(SafeSegment).ToArray();
        var directory = Path.Combine(new[] { UploadRootPath }.Concat(safeSegments).ToArray());
        var fullDirectory = Path.GetFullPath(directory);
        EnsureInsideUploadRoot(fullDirectory);
        return fullDirectory;
    }

    public string BuildRelativeUrl(IReadOnlyList<string> folderSegments, string fileName)
    {
        var safeSegments = folderSegments.Select(SafeSegment).ToArray();
        var safeFileName = SafeFileName(fileName);
        return PublicRequestPath + "/" + string.Join("/", safeSegments.Append(safeFileName));
    }

    public string? ToPhysicalPath(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
            return null;

        var url = relativeUrl.Trim().Replace('\\', '/');
        if (!url.StartsWith('/'))
            url = "/" + url;

        var publicRequestPath = PublicRequestPath;
        string relativePath;

        if (url.Equals(publicRequestPath, StringComparison.OrdinalIgnoreCase))
        {
            relativePath = string.Empty;
        }
        else if (url.StartsWith(publicRequestPath + "/", StringComparison.OrdinalIgnoreCase))
        {
            relativePath = url[(publicRequestPath.Length + 1)..];
        }
        else
        {
            return null;
        }

        var physicalPath = Path.GetFullPath(Path.Combine(
            UploadRootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

        EnsureInsideUploadRoot(physicalPath);
        return physicalPath;
    }

    public bool FileExists(string? relativeUrl)
    {
        var path = ToPhysicalPath(relativeUrl);
        return path is not null && File.Exists(path);
    }

    public void DeleteIfExists(string? relativeUrl)
    {
        var path = ToPhysicalPath(relativeUrl);
        if (path is null || !File.Exists(path)) return;

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // File đang được sử dụng; lần ghi đè tiếp theo sẽ tạo file mới.
        }
        catch (UnauthorizedAccessException)
        {
            // Không làm hỏng nghiệp vụ nếu hệ điều hành từ chối xóa file cũ.
        }
    }

    private void EnsureInsideUploadRoot(string fullPath)
    {
        var root = Path.GetFullPath(UploadRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(fullPath);

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!target.StartsWith(root, comparison) &&
            !string.Equals(
                target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                comparison))
        {
            throw new InvalidOperationException("Đường dẫn file upload không hợp lệ.");
        }
    }

    private static (byte[] Bytes, string Extension) DecodeDataUrl(string dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl) ||
            !dataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Chữ ký phải là dữ liệu ảnh PNG hoặc JPG.");
        }

        var comma = dataUrl.IndexOf(',');
        if (comma < 0)
            throw new InvalidOperationException("Dữ liệu chữ ký không hợp lệ.");

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(dataUrl[(comma + 1)..]);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Dữ liệu chữ ký không đúng định dạng Base64.");
        }

        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return (bytes, "png");
        }

        if (bytes.Length >= 3 &&
            bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return (bytes, "jpg");
        }

        throw new InvalidOperationException("Chữ ký phải là ảnh PNG hoặc JPG hợp lệ.");
    }

    private static string SafeSegment(string value)
    {
        var chars = value
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-')
            .ToArray();

        var result = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(result) ? "file" : result.ToLowerInvariant();
    }

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
            return $"file-{Guid.NewGuid():N}";

        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name
            .Select(character => invalid.Contains(character) || !(char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
                ? '-'
                : character)
            .ToArray())
            .Trim('-');

        return string.IsNullOrWhiteSpace(sanitized)
            ? $"file-{Guid.NewGuid():N}"
            : sanitized;
    }
}
