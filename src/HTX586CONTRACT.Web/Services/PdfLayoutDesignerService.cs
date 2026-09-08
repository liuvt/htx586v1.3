using System.Text.Json;
using System.Text.Json.Serialization;

namespace HTX586CONTRACT.Web.Services;

/// <summary>
/// Service quản lý template PDF và file layout.json cho màn hình kéo thả vị trí data.
/// Không tham gia xuất hợp đồng thật, chỉ dùng để chỉnh tọa độ/format layout.
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

    public async Task<PdfLayoutDesignerDocument> LoadAsync(
        PdfLayoutDesignerContractType contractType = PdfLayoutDesignerContractType.Passenger,
        CancellationToken cancellationToken = default)
    {
        var paths = ResolveDesignerPaths(contractType);

        if (!File.Exists(paths.TemplatePath))
            throw new FileNotFoundException($"Không tìm thấy PDF template tại '{paths.TemplatePath}'.", paths.TemplatePath);

        if (!File.Exists(paths.LayoutPath))
            throw new FileNotFoundException($"Không tìm thấy layout JSON tại '{paths.LayoutPath}'.", paths.LayoutPath);

        var layoutJson = await File.ReadAllTextAsync(paths.LayoutPath, cancellationToken);
        var layout = JsonSerializer.Deserialize<PdfTemplateLayoutDto>(layoutJson, ReadJsonOptions)
            ?? throw new InvalidOperationException("Không thể đọc layout JSON.");

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

        Directory.CreateDirectory(Path.GetDirectoryName(layoutPath)!);

        if (File.Exists(layoutPath))
        {
            var backupPath = Path.Combine(
                Path.GetDirectoryName(layoutPath)!,
                $"{Path.GetFileName(layoutPath)}.bak-{DateTime.Now:yyyyMMddHHmmss}");
            File.Copy(layoutPath, backupPath, overwrite: false);
            logger.LogInformation(
                "Đã backup layout PDF. ContractType={ContractType}, Backup={BackupPath}",
                contractType,
                backupPath);
        }

        var json = JsonSerializer.Serialize(layout, WriteJsonOptions);
        await File.WriteAllTextAsync(layoutPath, json, cancellationToken);

        logger.LogInformation(
            "Đã lưu layout PDF. ContractType={ContractType}, Layout={LayoutPath}",
            contractType,
            layoutPath);
    }

    public static string GetContractTypeName(PdfLayoutDesignerContractType contractType)
        => contractType switch
        {
            PdfLayoutDesignerContractType.Cargo => "Hợp đồng vận chuyển hàng hóa",
            _ => "Hợp đồng vận chuyển hành khách"
        };

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
            configuration[isCargo
                ? "DocumentGeneration:CargoContractLayoutPath"
                : "DocumentGeneration:ContractLayoutPath"],
            Path.Combine(
                "Templates",
                "Contracts",
                isCargo
                    ? "HopDongVanChuyenHangHoa.layout.json"
                    : "HopDongVanChuyenHanhKhach.layout.json"));

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
