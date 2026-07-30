namespace HTX586CONTRACT.Application.Abstractions;

public sealed record StoredUploadFile(
    string RelativeUrl,
    string Sha256Hash,
    DateTime SavedAt);

public interface IUploadFileStorage
{
    Task<StoredUploadFile> SaveImageDataUrlAsync(
        IReadOnlyList<string> folderSegments,
        string prefix,
        string dataUrl,
        CancellationToken cancellationToken = default);

    string GetPhysicalDirectory(IReadOnlyList<string> folderSegments);

    string BuildRelativeUrl(IReadOnlyList<string> folderSegments, string fileName);

    string? ToPhysicalPath(string? relativeUrl);

    bool FileExists(string? relativeUrl);

    void DeleteIfExists(string? relativeUrl);
}
