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
    ILogger<PdfLayoutDesignerService> logger)
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
        var paths = ResolveDesignerPaths(contractType);

        if (!File.Exists(paths.TemplatePath))
            throw new FileNotFoundException($"Không tìm thấy PDF template tại '{paths.TemplatePath}'.", paths.TemplatePath);

        if (!File.Exists(paths.LayoutPath))
            throw new FileNotFoundException($"Không tìm thấy layout JSON dùng chung tại '{paths.LayoutPath}'.", paths.LayoutPath);

        var bundle = await ReadBundleAsync(paths.LayoutPath, cancellationToken);
        var layout = SelectLayout(bundle, contractType)
            ?? throw new InvalidOperationException($"JSON dùng chung chưa có layout cho {GetContractTypeName(contractType)}.");

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
        var paths = ResolveDesignerPaths(contractType);
        var layoutPath = paths.LayoutPath;
        var layoutDirectory = Path.GetDirectoryName(layoutPath)!;

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

            var json = JsonSerializer.Serialize(bundle, WriteJsonOptions);
            var tempPath = Path.Combine(
                layoutDirectory,
                $".{Path.GetFileName(layoutPath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                await File.WriteAllTextAsync(tempPath, json, cancellationToken);
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
        var layoutJson = await File.ReadAllTextAsync(layoutPath, cancellationToken);
        return JsonSerializer.Deserialize<PdfTemplateLayoutBundleDto>(layoutJson, ReadJsonOptions)
            ?? throw new InvalidOperationException("Không thể đọc layout JSON dùng chung.");
    }

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
