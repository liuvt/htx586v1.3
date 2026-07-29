namespace HTX586CONTRACT.Application.Common;

/// <summary>
/// Chuẩn hóa các đường dẫn dữ liệu runtime để mọi lớp dùng chung một DataRoot.
/// </summary>
public static class StoragePathResolver
{
    public static string ResolveDataRootPath(string contentRootPath, string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(contentRootPath))
            throw new ArgumentException("ContentRootPath không hợp lệ.", nameof(contentRootPath));

        var normalizedContentRoot = Path.GetFullPath(contentRootPath);
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? new DataStorageOptions().RootPath
            : configuredPath.Trim();

        var resolved = Path.IsPathRooted(path)
            ? path
            : Path.Combine(normalizedContentRoot, path);

        var dataRoot = Path.GetFullPath(resolved);
        if (IsSameOrChildPath(dataRoot, normalizedContentRoot))
        {
            throw new InvalidOperationException(
                $"DataStorage:RootPath phải nằm ngoài thư mục source/publish. " +
                $"ContentRoot='{normalizedContentRoot}', DataRoot='{dataRoot}'.");
        }

        return dataRoot;
    }

    public static string ResolvePathUnderDataRoot(
        string contentRootPath,
        string? configuredDataRootPath,
        string? configuredChildPath,
        string defaultChildPath)
    {
        var dataRootPath = ResolveDataRootPath(contentRootPath, configuredDataRootPath);
        var childPath = string.IsNullOrWhiteSpace(configuredChildPath)
            ? defaultChildPath
            : configuredChildPath.Trim();

        if (Path.IsPathRooted(childPath))
            return Path.GetFullPath(childPath);

        var resolved = Path.GetFullPath(Path.Combine(dataRootPath, childPath));
        if (!IsSameOrChildPath(resolved, dataRootPath))
            throw new InvalidOperationException("Đường dẫn dữ liệu con không được phép thoát khỏi DataStorage:RootPath.");

        return resolved;
    }

    private static bool IsSameOrChildPath(string candidatePath, string parentPath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var candidate = Path.GetFullPath(candidatePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetFullPath(parentPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return string.Equals(candidate, parent, comparison) ||
               candidate.StartsWith(parent + Path.DirectorySeparatorChar, comparison);
    }
}
