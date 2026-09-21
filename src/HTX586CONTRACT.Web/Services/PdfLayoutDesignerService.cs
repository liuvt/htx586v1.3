using Microsoft.AspNetCore.Components.Authorization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HTX586CONTRACT.Web.Services;

/// <summary>
/// Service quản lý template PDF và một file layout JSON dùng chung cho cả
/// hợp đồng hành khách và hợp đồng hàng hóa.
/// </summary>
public sealed class PdfLayoutDesignerService(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILogger<PdfLayoutDesignerService> logger,
    AuthenticationStateProvider authenticationStateProvider)
{
    private static readonly JsonSerializerOptions ReadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    // Tránh hai phiên designer lưu đè lẫn nhau khi cùng chỉnh 2 loại HĐ.
    private static readonly SemaphoreSlim LayoutWriteLock = new(1, 1);

    public async Task<PdfLayoutDesignerDocument> LoadAsync(
        PdfLayoutDesignerContractType contractType = PdfLayoutDesignerContractType.Passenger,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnerAccessAsync();

        var paths = ResolveDesignerPaths(contractType);

        if (!File.Exists(paths.TemplatePath))
            throw new FileNotFoundException($"Không tìm thấy PDF template tại '{paths.TemplatePath}'.", paths.TemplatePath);

        if (!File.Exists(paths.LayoutPath))
            throw new FileNotFoundException($"Không tìm thấy layout JSON dùng chung tại '{paths.LayoutPath}'.", paths.LayoutPath);

        var bundle = await ReadBundleAsync(paths.LayoutPath, cancellationToken);
        var layout = SelectLayout(bundle, contractType)
            ?? throw new InvalidOperationException($"JSON dùng chung chưa có layout cho {GetContractTypeName(contractType)}.");

        ValidateLayout(layout, contractType);

        var pdfBytes = await File.ReadAllBytesAsync(paths.TemplatePath, cancellationToken);

        return new PdfLayoutDesignerDocument
        {
            ContractType = contractType,
            ContractTypeName = GetContractTypeName(contractType),
            TemplatePath = paths.TemplatePath,
            LayoutPath = paths.LayoutPath,
            TemplateBase64 = Convert.ToBase64String(pdfBytes),
            Layout = layout
        };
    }

    public async Task SaveLayoutAsync(
        PdfLayoutDesignerContractType contractType,
        PdfTemplateLayoutDto layout,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnerAccessAsync();

        var paths = ResolveDesignerPaths(contractType);
        var layoutPath = paths.LayoutPath;
        var layoutDirectory = Path.GetDirectoryName(layoutPath)!;

        NormalizeLayout(layout);
        ValidateLayout(layout, contractType);

        Directory.CreateDirectory(layoutDirectory);

        await LayoutWriteLock.WaitAsync(cancellationToken);
        try
        {
            var bundle = File.Exists(layoutPath)
                ? await ReadBundleAsync(layoutPath, cancellationToken)
                : new PdfTemplateLayoutBundleDto();

            if (contractType == PdfLayoutDesignerContractType.Cargo)
                bundle.Cargo = layout;
            else
                bundle.Passenger = layout;

            bundle.Version = Math.Max(1, bundle.Version);
            bundle.Description = string.IsNullOrWhiteSpace(bundle.Description)
                ? "Unified PDF layouts for passenger and cargo contracts"
                : bundle.Description;

            if (File.Exists(layoutPath))
            {
                var backupPath = Path.Combine(
                    layoutDirectory,
                    $"{Path.GetFileName(layoutPath)}.bak-{DateTime.Now:yyyyMMddHHmmssfff}");
                File.Copy(layoutPath, backupPath, overwrite: false);
                logger.LogInformation(
                    "Đã backup layout PDF dùng chung. ContractType={ContractType}, Backup={BackupPath}",
                    contractType,
                    backupPath);
            }

            ValidateBundle(bundle);

            var json = JsonSerializer.Serialize(bundle, WriteJsonOptions);
            var roundTripBundle = JsonSerializer.Deserialize<PdfTemplateLayoutBundleDto>(json, ReadJsonOptions)
                ?? throw new InvalidOperationException("JSON layout sau khi serialize không thể đọc lại.");
            ValidateBundle(roundTripBundle);

            var tempPath = Path.Combine(
                layoutDirectory,
                $".{Path.GetFileName(layoutPath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                await File.WriteAllTextAsync(tempPath, json, cancellationToken);

                // Đọc lại chính file tạm trước khi replace file đang chạy. Nhờ vậy một lỗi
                // ghi dở/encoding sẽ không làm hỏng layout chính và khiến Designer không vào lại được.
                var tempJson = await File.ReadAllTextAsync(tempPath, cancellationToken);
                var tempBundle = JsonSerializer.Deserialize<PdfTemplateLayoutBundleDto>(tempJson, ReadJsonOptions)
                    ?? throw new InvalidOperationException("File JSON tạm vừa ghi không thể đọc lại.");
                ValidateBundle(tempBundle);

                File.Move(tempPath, layoutPath, overwrite: true);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                    // Không che lỗi lưu chính.
                }
            }

            logger.LogInformation(
                "Đã lưu layout PDF vào JSON dùng chung. ContractType={ContractType}, Layout={LayoutPath}",
                contractType,
                layoutPath);
        }
        finally
        {
            LayoutWriteLock.Release();
        }
    }


    private async Task EnsureOwnerAccessAsync()
    {
        var auth = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = auth.User;

        if (user.Identity?.IsAuthenticated == true && user.IsInRole("Owner"))
            return;

        var role = user.IsInRole("Admin")
            ? "Admin"
            : user.IsInRole("VehicleOwner")
                ? "VehicleOwner"
                : "Unknown";

        logger.LogWarning(
            "Từ chối truy cập PDF Layout Designer. Role={Role}, User={User}",
            role,
            user.Identity?.Name ?? "anonymous");

        throw new UnauthorizedAccessException(
            "Chỉ tài khoản Owner mới có quyền sử dụng tính năng chỉnh sửa file PDF hợp đồng.");
    }

    public static string GetContractTypeName(PdfLayoutDesignerContractType contractType)
        => contractType switch
        {
            PdfLayoutDesignerContractType.Cargo => "Hợp đồng vận chuyển hàng hóa",
            _ => "Hợp đồng vận chuyển hành khách"
        };

    private static PdfTemplateLayoutDto? SelectLayout(
        PdfTemplateLayoutBundleDto bundle,
        PdfLayoutDesignerContractType contractType)
        => contractType == PdfLayoutDesignerContractType.Cargo
            ? bundle.Cargo
            : bundle.Passenger;

    private async Task<PdfTemplateLayoutBundleDto> ReadBundleAsync(
        string layoutPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var bundle = await ReadAndValidateBundleFileAsync(layoutPath, cancellationToken);
            return bundle;
        }
        catch (Exception mainError) when (mainError is JsonException or InvalidOperationException)
        {
            // Nếu lần lưu trước bị gián đoạn hoặc JSON bị chỉnh tay sai, thử bản backup mới nhất
            // để trang Designer vẫn mở được thay vì làm hỏng toàn bộ circuit.
            var directory = Path.GetDirectoryName(layoutPath);
            var fileName = Path.GetFileName(layoutPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                var backups = Directory
                    .EnumerateFiles(directory, $"{fileName}.bak-*")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .ToList();

                foreach (var backupPath in backups)
                {
                    try
                    {
                        var backupBundle = await ReadAndValidateBundleFileAsync(backupPath, cancellationToken);
                        logger.LogWarning(
                            mainError,
                            "Layout JSON chính bị lỗi. Tạm đọc backup để Designer vẫn hoạt động. Layout={LayoutPath}, Backup={BackupPath}",
                            layoutPath,
                            backupPath);
                        return backupBundle;
                    }
                    catch (Exception backupError) when (backupError is JsonException or InvalidOperationException)
                    {
                        logger.LogWarning(
                            backupError,
                            "Bỏ qua backup layout PDF không hợp lệ. Backup={BackupPath}",
                            backupPath);
                    }
                }
            }

            throw new InvalidOperationException(
                $"Không thể đọc layout JSON '{layoutPath}'. File chính không hợp lệ và không có backup hợp lệ để phục hồi. Chi tiết: {mainError.Message}",
                mainError);
        }
    }

    private static async Task<PdfTemplateLayoutBundleDto> ReadAndValidateBundleFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var bundle = JsonSerializer.Deserialize<PdfTemplateLayoutBundleDto>(json, ReadJsonOptions)
            ?? throw new InvalidOperationException("JSON layout rỗng hoặc không đúng cấu trúc bundle.");
        ValidateBundle(bundle);
        return bundle;
    }

    private static void ValidateBundle(PdfTemplateLayoutBundleDto bundle)
    {
        if (bundle.Passenger is null)
            throw new InvalidOperationException("JSON dùng chung thiếu node 'passenger'.");
        if (bundle.Cargo is null)
            throw new InvalidOperationException("JSON dùng chung thiếu node 'cargo'.");

        ValidateLayout(bundle.Passenger, PdfLayoutDesignerContractType.Passenger);
        ValidateLayout(bundle.Cargo, PdfLayoutDesignerContractType.Cargo);
    }

    public static void ValidateLayout(
        PdfTemplateLayoutDto layout,
        PdfLayoutDesignerContractType contractType)
    {
        var contractTypeName = GetContractTypeName(contractType);

        if (layout.Version < 1)
            throw new InvalidOperationException($"Layout {contractTypeName} có version không hợp lệ: {layout.Version}.");
        if (string.IsNullOrWhiteSpace(layout.TemplateFile))
            throw new InvalidOperationException($"Layout {contractTypeName} thiếu templateFile.");
        if (layout.TextFields is null || layout.ImageFields is null)
            throw new InvalidOperationException($"Layout {contractTypeName} thiếu textFields/imageFields.");
        if (layout.TextFields.Count == 0 && layout.ImageFields.Count == 0)
            throw new InvalidOperationException($"Layout {contractTypeName} không có field nào; có thể node JSON đã bị mất hoặc sai cấu trúc.");

        foreach (var field in layout.TextFields)
        {
            if (string.IsNullOrWhiteSpace(field.Key))
                throw new InvalidOperationException($"Layout {contractTypeName} có text field thiếu key.");
            if (field.Page < 1)
                throw new InvalidOperationException($"Text field '{field.Key}' có page không hợp lệ: {field.Page}.");
            if (!double.IsFinite(field.X) || !double.IsFinite(field.Y) ||
                !double.IsFinite(field.Width) || !double.IsFinite(field.Height) ||
                field.X < 0 || field.Y < 0 || field.Width <= 0 || field.Height <= 0)
                throw new InvalidOperationException($"Text field '{field.Key}' có tọa độ/kích thước không hợp lệ.");
            if (!float.IsFinite(field.FontSize) || !float.IsFinite(field.MinFontSize) ||
                field.FontSize <= 0 || field.MinFontSize <= 0 || field.MinFontSize > field.FontSize)
                throw new InvalidOperationException($"Text field '{field.Key}' có cỡ chữ không hợp lệ.");
            if (field.MaxLines < 1)
                throw new InvalidOperationException($"Text field '{field.Key}' có maxLines < 1.");
        }

        foreach (var field in layout.ImageFields)
        {
            if (string.IsNullOrWhiteSpace(field.Key))
                throw new InvalidOperationException($"Layout {contractTypeName} có image field thiếu key.");
            if (field.Page < 1)
                throw new InvalidOperationException($"Image field '{field.Key}' có page không hợp lệ: {field.Page}.");
            if (!double.IsFinite(field.X) || !double.IsFinite(field.Y) ||
                !double.IsFinite(field.Width) || !double.IsFinite(field.Height) ||
                field.X < 0 || field.Y < 0 || field.Width <= 0 || field.Height <= 0)
                throw new InvalidOperationException($"Image field '{field.Key}' có tọa độ/kích thước không hợp lệ.");
        }

        var duplicateText = layout.TextFields
            .GroupBy(x => $"{x.Page}:{x.Key.Trim()}", StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateText is not null)
            throw new InvalidOperationException($"Layout {contractTypeName} bị trùng text field '{duplicateText.Key}'.");

        var duplicateImage = layout.ImageFields
            .GroupBy(x => $"{x.Page}:{x.Key.Trim()}", StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateImage is not null)
            throw new InvalidOperationException($"Layout {contractTypeName} bị trùng image field '{duplicateImage.Key}'.");
    }

    public static void NormalizeLayout(PdfTemplateLayoutDto layout)
    {
        layout.FontFamily = string.IsNullOrWhiteSpace(layout.FontFamily) ? "Times New Roman" : layout.FontFamily.Trim();
        layout.FallbackFontFamilies ??= [];
        layout.TextFields ??= [];
        layout.ImageFields ??= [];

        foreach (var field in layout.TextFields)
        {
            field.Key = field.Key?.Trim() ?? string.Empty;
            field.X = Math.Max(0, RoundValue(field.X));
            field.Y = Math.Max(0, RoundValue(field.Y));
            field.Width = Math.Max(2, RoundValue(field.Width));
            field.Height = Math.Max(2, RoundValue(field.Height));
            field.FontSize = Math.Max(1, field.FontSize);
            field.MinFontSize = Math.Clamp(field.MinFontSize, 1, field.FontSize);
            field.MaxLines = Math.Max(1, field.MaxLines);
            field.Alignment = NormalizeHorizontalAlignment(field.Alignment);
            field.VerticalAlignment = NormalizeVerticalAlignment(field.VerticalAlignment);
        }

        foreach (var field in layout.ImageFields)
        {
            field.Key = field.Key?.Trim() ?? string.Empty;
            field.X = Math.Max(0, RoundValue(field.X));
            field.Y = Math.Max(0, RoundValue(field.Y));
            field.Width = Math.Max(2, RoundValue(field.Width));
            field.Height = Math.Max(2, RoundValue(field.Height));
            field.Fit = string.IsNullOrWhiteSpace(field.Fit) ? "Contain" : field.Fit.Trim();
        }
    }

    private static string NormalizeHorizontalAlignment(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "center" => "Center",
            "right" => "Right",
            _ => "Left"
        };

    private static string NormalizeVerticalAlignment(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "top" => "Top",
            "bottom" => "Bottom",
            "middle" => "Center",
            "center" => "Center",
            _ => "Center"
        };

    private static double RoundValue(double value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private DesignerPaths ResolveDesignerPaths(PdfLayoutDesignerContractType contractType)
    {
        var isCargo = contractType == PdfLayoutDesignerContractType.Cargo;

        var templatePath = ResolveContentPath(
            configuration[isCargo
                ? "DocumentGeneration:CargoContractTemplatePath"
                : "DocumentGeneration:ContractTemplatePath"],
            Path.Combine(
                "Templates",
                "Contracts",
                isCargo
                    ? "HopDongVanChuyenHangHoa.template.pdf"
                    : "HopDongVanChuyenHanhKhach.template.pdf"));

        var layoutPath = ResolveContentPath(
            configuration["DocumentGeneration:ContractLayoutsPath"],
            Path.Combine("Templates", "Contracts", "HopDongVanChuyen.layout.json"));

        return new DesignerPaths(templatePath, layoutPath);
    }

    private string ResolveContentPath(string? configuredPath, string defaultRelativePath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? defaultRelativePath
            : configuredPath.Trim();

        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, path));
    }

    private sealed record DesignerPaths(string TemplatePath, string LayoutPath);
}

public enum PdfLayoutDesignerContractType
{
    Passenger = 1,
    Cargo = 2
}

public sealed class PdfLayoutDesignerDocument
{
    public PdfLayoutDesignerContractType ContractType { get; set; } = PdfLayoutDesignerContractType.Passenger;
    public string ContractTypeName { get; set; } = string.Empty;
    public string TemplatePath { get; set; } = string.Empty;
    public string LayoutPath { get; set; } = string.Empty;
    public string TemplateFileName => Path.GetFileName(TemplatePath);
    public string LayoutFileName => Path.GetFileName(LayoutPath);
    public string TemplateBase64 { get; set; } = string.Empty;
    public PdfTemplateLayoutDto Layout { get; set; } = new();
}

public sealed class PdfTemplateLayoutBundleDto
{
    public int Version { get; set; } = 1;
    public string Description { get; set; } = "Unified PDF layouts for passenger and cargo contracts";
    public PdfTemplateLayoutDto Passenger { get; set; } = new();
    public PdfTemplateLayoutDto Cargo { get; set; } = new();
}

public sealed class PdfTemplateLayoutDto
{
    public int Version { get; set; }
    public string CoordinateSystem { get; set; } = "PDF points, origin top-left, A4 595.28 x 841.89";
    public string TemplateFile { get; set; } = "HopDongVanChuyenHanhKhach.template.pdf";
    public string FontFamily { get; set; } = "Times New Roman";
    public List<string> FallbackFontFamilies { get; set; } = [];
    public List<PdfTextFieldLayoutDto> TextFields { get; set; } = [];
    public List<PdfImageFieldLayoutDto> ImageFields { get; set; } = [];
}

public sealed class PdfTextFieldLayoutDto
{
    public string Key { get; set; } = string.Empty;
    public int Page { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public float FontSize { get; set; } = 11f;
    public float MinFontSize { get; set; } = 8f;
    public int MaxLines { get; set; } = 1;
    public string Alignment { get; set; } = "Left";
    public string VerticalAlignment { get; set; } = "Center";
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Uppercase { get; set; }
    public bool ClearBackground { get; set; }
}

public sealed class PdfImageFieldLayoutDto
{
    public string Key { get; set; } = string.Empty;
    public int Page { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Fit { get; set; } = "Contain";
}

public sealed class PdfLayoutDesignerFieldUpdate
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int Page { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}
