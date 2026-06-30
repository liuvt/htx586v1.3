using System.Security.Cryptography;

namespace HTX586CONTRACT.Web.Services;

public sealed record StoredSignatureFile(string RelativeUrl, string Sha256Hash, DateTime SavedAt);

/// <summary>
/// Lưu ảnh chữ ký PNG từ canvas vào wwwroot/uploads và trả về URL tương đối.
/// Dùng chung cho chữ ký cố định danh mục và chữ ký khách hàng theo hợp đồng.
/// </summary>
public sealed class SignatureFileStorage(IWebHostEnvironment environment)
{
    private const int MaxSignatureBytes = 2 * 1024 * 1024;

    public async Task<StoredSignatureFile> SavePngDataUrlAsync(
        IReadOnlyList<string> folderSegments,
        string prefix,
        string dataUrl,
        CancellationToken ct = default)
    {
        if (folderSegments.Count == 0)
            throw new InvalidOperationException("Thiếu thư mục lưu chữ ký.");

        if (string.IsNullOrWhiteSpace(prefix))
            prefix = "signature";

        var bytes = DecodeDataUrl(dataUrl);
        if (bytes.Length == 0 || bytes.Length > MaxSignatureBytes)
            throw new InvalidOperationException("Dung lượng chữ ký không hợp lệ hoặc vượt quá 2 MB.");

        var safePrefix = SafeSegment(prefix);
        var safeFolderSegments = folderSegments.Select(SafeSegment).ToArray();
        var fileName = $"{safePrefix}-{Guid.NewGuid():N}.png";
        var physicalDirectory = Path.Combine(
            new[] { environment.WebRootPath, "uploads" }.Concat(safeFolderSegments).ToArray());

        Directory.CreateDirectory(physicalDirectory);

        var physicalPath = Path.Combine(physicalDirectory, fileName);
        var tempPath = Path.Combine(physicalDirectory, $".{fileName}.uploading");

        await File.WriteAllBytesAsync(tempPath, bytes, ct);
        File.Move(tempPath, physicalPath, overwrite: true);

        var relativeUrl = "/uploads/" + string.Join('/', safeFolderSegments) + "/" + fileName;
        return new StoredSignatureFile(
            relativeUrl,
            Convert.ToHexString(SHA256.HashData(bytes)),
            DateTime.UtcNow);
    }

    public string? ToPhysicalPath(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
            return null;

        return Path.Combine(
            environment.WebRootPath,
            relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    }

    private static byte[] DecodeDataUrl(string dataUrl)
    {
        var comma = dataUrl.IndexOf(',');
        if (comma < 0)
            throw new InvalidOperationException("Dữ liệu chữ ký không hợp lệ.");

        var header = dataUrl[..comma];
        if (!header.Contains("image/png", StringComparison.OrdinalIgnoreCase) &&
            !header.Contains("image/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chữ ký phải là dữ liệu ảnh.");

        try
        {
            return Convert.FromBase64String(dataUrl[(comma + 1)..]);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Dữ liệu chữ ký không đúng định dạng Base64.");
        }
    }

    private static string SafeSegment(string value)
    {
        var chars = value
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-')
            .ToArray();

        var result = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(result) ? "signature" : result.ToLowerInvariant();
    }
}
