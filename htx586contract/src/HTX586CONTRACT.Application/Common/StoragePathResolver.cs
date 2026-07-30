namespace HTX586CONTRACT.Application.Common;

public static class StoragePathResolver
{
    public const string SourceDirectoryName = "htx586contract";
    public const string DataDirectoryName = "htx586contract_data";
    public const string UploadDirectoryName = "upload";
    public const string DataProtectionDirectoryName = "dataprotection-keys";
    public const string PublicUploadPath = "/upload";

    public static string ResolveDataRootPath(string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(contentRootPath))
            throw new ArgumentException("ContentRootPath không hợp lệ.", nameof(contentRootPath));

        var sourceRoot = FindSourceRoot(Path.GetFullPath(contentRootPath));
        var parent = Directory.GetParent(sourceRoot)?.FullName
            ?? throw new InvalidOperationException("Không xác định được thư mục cha của source.");

        var dataRoot = Path.GetFullPath(Path.Combine(parent, DataDirectoryName));
        if (IsSameOrChildPath(dataRoot, sourceRoot))
            throw new InvalidOperationException("Thư mục dữ liệu phải nằm ngoài source/publish.");

        return dataRoot;
    }

    public static string ResolveUploadRootPath(string contentRootPath)
        => Path.Combine(ResolveDataRootPath(contentRootPath), UploadDirectoryName);

    public static string ResolveDataProtectionKeysPath(string contentRootPath)
        => Path.Combine(ResolveDataRootPath(contentRootPath), DataProtectionDirectoryName);

    private static string FindSourceRoot(string contentRoot)
    {
        var directory = new DirectoryInfo(contentRoot);
        while (directory is not null)
        {
            if (directory.Name.Equals(SourceDirectoryName, StringComparison.OrdinalIgnoreCase))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Source phải nằm trong thư mục có tên '{SourceDirectoryName}' để xác định thư mục dữ liệu cùng cấp.");
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

        return candidate.Equals(parent, comparison) ||
               candidate.StartsWith(parent + Path.DirectorySeparatorChar, comparison);
    }
}
