using HTX586CONTRACT.Application.Common;

namespace HTX586CONTRACT.Web.Services;

public static class UploadPathResolver
{
    public static string ResolveUploadRootPath(
        string contentRootPath,
        string? dataRootPath,
        string? configuredUploadPath)
    {
        return StoragePathResolver.ResolvePathUnderDataRoot(
            contentRootPath,
            dataRootPath,
            configuredUploadPath,
            new FileStorageOptions().UploadRootPath);
    }

    public static string NormalizeRequestPath(string? requestPath)
    {
        if (string.IsNullOrWhiteSpace(requestPath))
            return "/uploads";

        var normalized = requestPath.Trim().Replace('\\', '/');
        if (!normalized.StartsWith('/'))
            normalized = "/" + normalized;

        return normalized.TrimEnd('/');
    }
}
