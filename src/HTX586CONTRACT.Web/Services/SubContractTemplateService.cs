using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HTX586CONTRACT.Application.Contracts;
using HTX586CONTRACT.Domain.Companies;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Enums;
using Microsoft.AspNetCore.Components.Authorization;

namespace HTX586CONTRACT.Web.Services;

public sealed class SubContractPlaceholderInfo
{
    public string Token { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string SampleValue { get; init; } = string.Empty;
}

public sealed class SubContractTemplateDto
{
    public Guid? CompanyProfileId { get; set; }
    public string TemplateName { get; set; } = "Mẫu Hợp đồng dịch vụ vận tải";
    public string UnitHeaderTitle { get; set; } = string.Empty;
    public string ContractNumberLine { get; set; } = "Số:{{SUB_CONTRACT_NUMBER}}";
    public string DocumentTitle { get; set; } = "HỢP ĐỒNG DỊCH VỤ VẬN TẢI";
    public string LegalBasesText { get; set; } = string.Empty;
    public string IntroLocationText { get; set; } = string.Empty;
    public string PartyAText { get; set; } = string.Empty;
    public string PartyBText { get; set; } = string.Empty;
    public string PreambleText { get; set; } = string.Empty;
    public string Article1Text { get; set; } = string.Empty;
    public string Article2Text { get; set; } = string.Empty;
    public string Article3PartyAText { get; set; } = string.Empty;
    public string Article3PartyBText { get; set; } = string.Empty;
    public string Article4Text { get; set; } = string.Empty;
    public string Article5Text { get; set; } = string.Empty;
    public string SignatureLeftTitle { get; set; } = "ĐẠI DIỆN BÊN A";
    public string SignatureLeftSignerName { get; set; } = string.Empty;
    public string SignatureRightTitle { get; set; } = "ĐẠI DIỆN BÊN B";
    public string? UploadedDocxFileName { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SubContractTemplateBundleDto
{
    public int Version { get; set; } = 2;
    public SubContractTemplateDto DefaultCaoSon { get; set; } = new();
    public SubContractTemplateDto DefaultMekongXanh { get; set; } = new();
    public Dictionary<string, SubContractTemplateDto> CompanyTemplates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Quản lý Mẫu Hợp đồng phụ (Hợp đồng dịch vụ vận tải) cứng riêng cho từng Đơn vị / Khu HTX.
/// Kết hợp Hướng 1 (Soạn thảo trực tiếp trên Web) và Hướng 3 (Nhập/Xuất file Word .docx).
/// Chỉ tài khoản Owner mới có quyền chỉnh sửa mẫu.
/// </summary>
public sealed class SubContractTemplateService(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILogger<SubContractTemplateService> logger)
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

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

    public static IReadOnlyList<SubContractPlaceholderInfo> SupportedPlaceholders { get; } =
    [
        new() { Token = "{{SUB_CONTRACT_NUMBER}}", Label = "Số Hợp đồng phụ", SampleValue = "0709. 02HTXCSRCN" },
        new() { Token = "{{MAIN_CONTRACT_NUMBER}}", Label = "Số Hợp đồng chính", SampleValue = "65E00389-02" },
        new() { Token = "{{SIGN_DAY}}", Label = "Ngày ký (dd)", SampleValue = "07" },
        new() { Token = "{{SIGN_MONTH}}", Label = "Tháng ký (MM)", SampleValue = "09" },
        new() { Token = "{{SIGN_YEAR}}", Label = "Năm ký (yyyy)", SampleValue = "2026" },
        new() { Token = "{{EFFECTIVE_FROM}}", Label = "Ngày hiệu lực từ", SampleValue = "07 / 09 / 2026" },
        new() { Token = "{{EFFECTIVE_TO}}", Label = "Ngày hết hiệu lực (4 năm)", SampleValue = "07 / 09 / 2030" },
        new() { Token = "{{OWNER_NAME}}", Label = "Tên Chủ phương tiện (Bên B)", SampleValue = "PHAN THỊ VIỆT HÀ" },
        new() { Token = "{{OWNER_CITIZEN_ID}}", Label = "CCCD Chủ phương tiện (Bên B)", SampleValue = "092162002859" },
        new() { Token = "{{OWNER_ISSUED_DATE}}", Label = "Ngày cấp CCCD Bên B", SampleValue = "29/04/2021" },
        new() { Token = "{{OWNER_ISSUED_PLACE}}", Label = "Nơi cấp CCCD Bên B", SampleValue = "Cục Cảnh Sát QLHC về TTXH" },
        new() { Token = "{{OWNER_ADDRESS}}", Label = "Địa chỉ Chủ phương tiện (Bên B)", SampleValue = "Kv Thới Hưng, Long Hưng, Ô Môn, Cần Thơ" },
        new() { Token = "{{OWNER_PHONE}}", Label = "SĐT Chủ phương tiện (Bên B)", SampleValue = "0909123456" },
        new() { Token = "{{VEHICLE_PLATE}}", Label = "Biển số xe", SampleValue = "65E-003.89" },
        new() { Token = "{{VEHICLE_BRAND}}", Label = "Nhãn hiệu xe", SampleValue = "FORD" },
        new() { Token = "{{SEAT_COUNT}}", Label = "Số chỗ", SampleValue = "16" },
        new() { Token = "{{VEHICLE_CHASSIS}}", Label = "Số khung", SampleValue = "P7P36941DURATORQ4D225H" },
        new() { Token = "{{VEHICLE_ENGINE}}", Label = "Số máy", SampleValue = "RL3DLT6MMPTR56568" },
        new() { Token = "{{VEHICLE_TYPE}}", Label = "Loại phương tiện", SampleValue = "XE HỢP ĐỒNG" },
        new() { Token = "{{COMPANY_NAME}}", Label = "Tên Đơn vị (Bên A)", SampleValue = "CN HTX CÔNG NGHỆ VT CAO SƠN CAR TẠI CẦN THƠ" },
        new() { Token = "{{COMPANY_ADDRESS}}", Label = "Địa chỉ trụ sở Bên A", SampleValue = "Số 47 đường số 12, KTĐC Thới Nhựt 2, Phường Tân An, Thành phố Cần Thơ" },
        new() { Token = "{{COMPANY_PHONE}}", Label = "Số điện thoại Bên A", SampleValue = "0925351111" },
        new() { Token = "{{COMPANY_TAX_CODE}}", Label = "Mã số thuế Bên A", SampleValue = "0319340385-001" },
        new() { Token = "{{COMPANY_LICENSE}}", Label = "Số GPKDVT / ĐKKD Bên A", SampleValue = "922643479/GPKDVT" },
        new() { Token = "{{COMPANY_LICENSE_PLACE}}", Label = "Nơi cấp GPKDVT Bên A", SampleValue = "Sở Xây dựng Thành phố Cần Thơ" },
        new() { Token = "{{COMPANY_REPRESENTATIVE}}", Label = "Người đại diện Bên A", SampleValue = "Nguyễn Trọng Hậu" },
        new() { Token = "{{COMPANY_REP_POSITION}}", Label = "Chức vụ đại diện Bên A", SampleValue = "Giám đốc." },
        new() { Token = "{{COMPANY_REP_CITIZEN_ID}}", Label = "CCCD đại diện Bên A", SampleValue = "82203018675" },
        new() { Token = "{{COMPANY_REP_ISSUED_DATE}}", Label = "Ngày cấp CCCD Bên A", SampleValue = "19/01/2026" },
        new() { Token = "{{COMPANY_REP_ISSUED_PLACE}}", Label = "Nơi cấp CCCD Bên A", SampleValue = "Bộ Công An" }
    ];

    public string GetTemplatesJsonPath()
    {
        var configured = configuration["DocumentGeneration:SubContractTemplatesPath"];
        var relative = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine("Templates", "Contracts", "SubContractTemplates.json")
            : configured.Trim();

        return Path.IsPathRooted(relative)
            ? relative
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, relative));
    }

    public SubContractTemplateDto GetTemplateForCompany(
        Guid? companyProfileId,
        string? companyName,
        string? taxCode,
        CompanyProfile? companyEntity = null)
    {
        var bundle = LoadBundle();
        if (companyProfileId.HasValue &&
            bundle.CompanyTemplates.TryGetValue(companyProfileId.Value.ToString("N"), out var custom))
        {
            return Clone(custom);
        }

        if (SubContractHelper.IsCaoSonUnit(companyName, taxCode))
        {
            var caoSon = Clone(bundle.DefaultCaoSon);
            caoSon.CompanyProfileId = companyProfileId;
            return caoSon;
        }

        if (SubContractHelper.IsMekongXanhUnit(companyName, taxCode))
        {
            var mekong = Clone(bundle.DefaultMekongXanh);
            mekong.CompanyProfileId = companyProfileId;
            return mekong;
        }

        if (companyEntity is not null || !string.IsNullOrWhiteSpace(companyName))
        {
            return CreateFixedUnitTemplate(companyEntity, companyProfileId, companyName, taxCode);
        }

        var fallback = Clone(bundle.DefaultMekongXanh);
        fallback.CompanyProfileId = companyProfileId;
        return fallback;
    }

    public bool HasCustomCompanyTemplate(Guid companyProfileId)
    {
        var bundle = LoadBundle();
        return bundle.CompanyTemplates.ContainsKey(companyProfileId.ToString("N"));
    }

    public async Task SaveCompanyTemplateAsync(
        AuthenticationStateProvider authStateProvider,
        Guid? companyProfileId,
        SubContractTemplateDto template,
        string? companyName = null,
        string? taxCode = null,
        CancellationToken ct = default)
    {
        await EnsureOwnerAccessAsync(authStateProvider);

        var path = GetTemplatesJsonPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await WriteLock.WaitAsync(ct);
        try
        {
            var bundle = LoadBundle();
            template.CompanyProfileId = companyProfileId;
            template.UpdatedAtUtc = DateTime.UtcNow;

            if (companyProfileId.HasValue)
            {
                bundle.CompanyTemplates[companyProfileId.Value.ToString("N")] = Clone(template);
            }
            else if (SubContractHelper.IsCaoSonUnit(companyName, taxCode))
            {
                bundle.DefaultCaoSon = Clone(template);
            }
            else
            {
                bundle.DefaultMekongXanh = Clone(template);
            }

            await PersistBundleAsync(path, bundle, ct);
            logger.LogInformation(
                "Owner đã lưu mẫu Hợp đồng phụ cứng riêng cho đơn vị. CompanyProfileId={CompanyProfileId}, Path={Path}",
                companyProfileId,
                path);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public async Task<SubContractTemplateDto> ResetCompanyTemplateToDefaultAsync(
        AuthenticationStateProvider authStateProvider,
        Guid? companyProfileId,
        string? companyName,
        string? taxCode,
        CompanyProfile? companyEntity = null,
        CancellationToken ct = default)
    {
        await EnsureOwnerAccessAsync(authStateProvider);

        var path = GetTemplatesJsonPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await WriteLock.WaitAsync(ct);
        try
        {
            var bundle = LoadBundle();
            var isCaoSon = SubContractHelper.IsCaoSonUnit(companyName, taxCode);
            var isMekong = SubContractHelper.IsMekongXanhUnit(companyName, taxCode);

            SubContractTemplateDto defaultPreset = isCaoSon
                ? CreateDefaultPreset(isCaoSon: true)
                : isMekong
                    ? CreateDefaultPreset(isCaoSon: false)
                    : CreateFixedUnitTemplate(companyEntity, companyProfileId, companyName, taxCode);

            if (companyProfileId.HasValue)
            {
                defaultPreset.CompanyProfileId = companyProfileId;
                bundle.CompanyTemplates[companyProfileId.Value.ToString("N")] = Clone(defaultPreset);
            }
            else if (isCaoSon)
            {
                bundle.DefaultCaoSon = Clone(defaultPreset);
            }
            else
            {
                bundle.DefaultMekongXanh = Clone(defaultPreset);
            }

            await PersistBundleAsync(path, bundle, ct);
            return defaultPreset;
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public async Task<SubContractTemplateDto> ImportFromDocxAsync(
        AuthenticationStateProvider authStateProvider,
        Guid? companyProfileId,
        Stream docxStream,
        string originalFileName,
        string? companyName,
        string? taxCode,
        CancellationToken ct = default)
    {
        await EnsureOwnerAccessAsync(authStateProvider);

        using var memory = new MemoryStream();
        await docxStream.CopyToAsync(memory, ct);
        memory.Position = 0;

        List<string> paragraphs;
        try
        {
            using var wordDoc = WordprocessingDocument.Open(memory, false);
            var body = wordDoc.MainDocumentPart?.Document?.Body
                ?? throw new InvalidOperationException("File Word (.docx) không có nội dung văn bản.");

            paragraphs = body.Descendants<Paragraph>()
                .Select(p => p.InnerText?.Trim() ?? string.Empty)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("Không thể đọc file Word (.docx). Vui lòng chọn đúng định dạng .docx hợp lệ.", ex);
        }

        if (paragraphs.Count == 0)
            throw new InvalidOperationException("File Word (.docx) không chứa đoạn văn bản nào.");

        var subContractsDir = Path.Combine(Path.GetDirectoryName(GetTemplatesJsonPath())!, "SubContracts");
        Directory.CreateDirectory(subContractsDir);
        var keySegment = companyProfileId?.ToString("N") ?? (SubContractHelper.IsCaoSonUnit(companyName, taxCode) ? "default-caoson" : "default-mekongxanh");
        var storedDocxName = $"SubContract_{keySegment}_{DateTime.UtcNow:yyyyMMddHHmmss}.docx";
        var storedDocxPath = Path.Combine(subContractsDir, storedDocxName);
        await File.WriteAllBytesAsync(storedDocxPath, memory.ToArray(), ct);

        var current = GetTemplateForCompany(companyProfileId, companyName, taxCode);
        ParseParagraphsIntoTemplate(paragraphs, current);
        current.UploadedDocxFileName = originalFileName;
        current.UpdatedAtUtc = DateTime.UtcNow;

        await SaveCompanyTemplateAsync(authStateProvider, companyProfileId, current, companyName, taxCode, ct);
        return current;
    }

    public byte[] ExportTemplateAsDocxBytes(
        SubContractTemplateDto template,
        IReadOnlyDictionary<string, string>? replacements = null)
    {
        string R(string text)
            => replacements is null ? text : ReplacePlaceholders(text, replacements);

        using var stream = new MemoryStream();
        using (var wordDoc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = new Body();

            var headerTable = new Table(
                new TableProperties(
                    new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.None },
                        new BottomBorder { Val = BorderValues.None },
                        new LeftBorder { Val = BorderValues.None },
                        new RightBorder { Val = BorderValues.None },
                        new InsideHorizontalBorder { Val = BorderValues.None },
                        new InsideVerticalBorder { Val = BorderValues.None })));

            var headerRow = new TableRow();
            headerRow.Append(
                CreateTableCell(
                    [
                        (R(template.UnitHeaderTitle), true, false, "21"),
                        ("-----------------------", false, false, "20"),
                        (R(template.ContractNumberLine), false, false, "22")
                    ]),
                CreateTableCell(
                    [
                        ("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", true, false, "21"),
                        ("Độc lập - Tự do - Hạnh phúc", true, false, "22"),
                        ("--------------------", false, false, "20")
                    ]));
            headerTable.Append(headerRow);
            body.Append(headerTable);

            AppendParagraph(body, string.Empty, bold: false, italic: false, center: true, fontSizeHalfPt: "22");
            AppendParagraph(body, R(template.DocumentTitle), bold: true, italic: false, center: true, fontSizeHalfPt: "28");

            AppendMultiLineBlock(body, R(template.LegalBasesText), bold: false, italic: true);
            AppendMultiLineBlock(body, R(template.IntroLocationText), bold: false, italic: false);

            AppendParagraph(body, "Bên A : ( Bên cung ứng dịch vụ vận tải)", bold: true, italic: false);
            AppendMultiLineBlock(body, R(template.PartyAText), bold: false, italic: false);

            AppendParagraph(body, "Bên B : ( Bên sử dụng dịch vụ vận tải )", bold: true, italic: false);
            AppendMultiLineBlock(body, R(template.PartyBText), bold: false, italic: false);

            AppendMultiLineBlock(body, R(template.PreambleText), bold: false, italic: false);

            AppendParagraph(body, "Điều 1: Nội dung sản phẩm dịch vụ", bold: true, italic: false);
            AppendMultiLineBlock(body, R(template.Article1Text), bold: false, italic: false);

            AppendParagraph(body, "Điều 2: Phí dịch vụ và phương thức thanh toán khi thành viên nhận dịch vụ hợp tác xã phải trả cho Hợp Tác Xã một khoản chi phí:", bold: true, italic: false);
            AppendMultiLineBlock(body, R(template.Article2Text), bold: false, italic: false);

            AppendParagraph(body, "Điều 3: Trách nhiệm và quyền hạn của các bên:", bold: true, italic: false);
            AppendParagraph(body, "1. Trách nhiệm và quyền hạn của bên A:", bold: true, italic: false);
            AppendMultiLineBlock(body, R(template.Article3PartyAText), bold: false, italic: false);
            AppendParagraph(body, "2. Trách nhiệm và quyền hạn của bên B:", bold: true, italic: false);
            AppendMultiLineBlock(body, R(template.Article3PartyBText), bold: false, italic: false);

            AppendParagraph(body, "Điều 4: Các thỏa thuận khác", bold: true, italic: false);
            AppendMultiLineBlock(body, R(template.Article4Text), bold: false, italic: false);

            AppendParagraph(body, "Điều 5: Thời hạn hiệu lực hợp đồng", bold: true, italic: false);
            AppendMultiLineBlock(body, R(template.Article5Text), bold: false, italic: false);

            AppendParagraph(body, string.Empty, bold: false, italic: false);

            var leftSignerSource = string.IsNullOrWhiteSpace(template.SignatureLeftSignerName)
                ? "{{COMPANY_REPRESENTATIVE}}"
                : template.SignatureLeftSignerName;
            var repA = R(leftSignerSource).ToUpper(CultureInfo.GetCultureInfo("vi-VN"));
            var repB = R("{{OWNER_NAME}}").ToUpper(CultureInfo.GetCultureInfo("vi-VN"));

            var sigTable = new Table(
                new TableProperties(
                    new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.None },
                        new BottomBorder { Val = BorderValues.None },
                        new LeftBorder { Val = BorderValues.None },
                        new RightBorder { Val = BorderValues.None },
                        new InsideHorizontalBorder { Val = BorderValues.None },
                        new InsideVerticalBorder { Val = BorderValues.None })));

            var sigRow = new TableRow();
            sigRow.Append(
                CreateTableCell(
                    [
                        (R(template.SignatureLeftTitle), true, false, "22"),
                        (string.Empty, false, false, "22"),
                        (string.Empty, false, false, "22"),
                        (string.Empty, false, false, "22"),
                        (repA, true, false, "22")
                    ]),
                CreateTableCell(
                    [
                        (R(template.SignatureRightTitle), true, false, "22"),
                        (string.Empty, false, false, "22"),
                        (string.Empty, false, false, "22"),
                        (string.Empty, false, false, "22"),
                        (repB, true, false, "22")
                    ]));
            sigTable.Append(sigRow);
            body.Append(sigTable);

            body.Append(new SectionProperties(
                new PageSize { Width = 11906U, Height = 16838U },
                new PageMargin { Top = 850, Right = 850, Bottom = 850, Left = 1134 }));

            mainPart.Document.Append(body);
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    public static Dictionary<string, string> BuildContractPlaceholders(Contract contract)
    {
        var snapshot = ContractSnapshotData.FromJson(contract.ContractDataJson);
        var company = contract.CompanyProfile;
        var vehicle = contract.Vehicle;
        var driver = contract.Driver;

        var rawCompanyName = snapshot is not null
            ? First(snapshot.Company.Name, contract.CompanyNameSnapshot, company?.CompanyName, "...")
            : First(contract.CompanyNameSnapshot, company?.CompanyName, "...");
        var rawCompanyTaxCode = snapshot is not null
            ? First(snapshot.Company.TaxCode, contract.CompanyTaxCodeSnapshot, company?.TaxCode, "...")
            : First(contract.CompanyTaxCodeSnapshot, company?.TaxCode, "...");

        var fixedUnit = SubContractHelper.ResolveFixedUnitProfile(
            rawCompanyName,
            rawCompanyTaxCode,
            fallbackAddress: snapshot?.Company.Address ?? contract.CompanyAddressSnapshot ?? company?.Address,
            fallbackPhone: snapshot?.Company.PhoneNumber ?? company?.PhoneNumber,
            fallbackLicenseNumber: snapshot?.Company.BusinessLicenseNumber ?? company?.BusinessLicenseNumber,
            fallbackLicensePlace: snapshot?.Company.BusinessLicenseIssuedPlace ?? company?.BusinessLicenseIssuedPlace,
            fallbackRepName: snapshot?.Company.RepresentativeName ?? contract.CompanyRepresentativeSnapshot ?? company?.RepresentativeName,
            fallbackRepPosition: snapshot?.Company.RepresentativePosition ?? contract.CompanyRepresentativePositionSnapshot ?? company?.RepresentativePosition,
            fallbackRepCitizenId: snapshot?.Company.RepresentativeCitizenId ?? company?.RepresentativeCitizenId,
            fallbackRepCitizenIssuedDate: snapshot?.Company.RepresentativeCitizenIdIssuedDate ?? company?.RepresentativeCitizenIdIssuedDate,
            fallbackRepCitizenIssuedPlace: snapshot?.Company.RepresentativeCitizenIdIssuedPlace ?? company?.RepresentativeCitizenIdIssuedPlace);

        var ownerName = snapshot is not null
            ? First(snapshot.Vehicle.OwnerName, contract.VehicleOwnerNameSnapshot, vehicle?.OwnerName, driver?.FullName, "...")
            : First(contract.VehicleOwnerNameSnapshot, vehicle?.OwnerName, driver?.FullName, "...");
        var ownerCitizenId = snapshot is not null
            ? First(snapshot.Vehicle.OwnerCitizenId, contract.VehicleOwnerCitizenIdSnapshot, vehicle?.OwnerCitizenId, driver?.CitizenId, "...")
            : First(contract.VehicleOwnerCitizenIdSnapshot, vehicle?.OwnerCitizenId, driver?.CitizenId, "...");
        var ownerIssuedDate = FormatDateOnly(snapshot is not null
            ? snapshot.Vehicle.OwnerCitizenIdIssuedDate ?? vehicle?.OwnerCitizenIdIssuedDate ?? driver?.CitizenIdIssuedDate
            : vehicle?.OwnerCitizenIdIssuedDate ?? driver?.CitizenIdIssuedDate);
        var ownerIssuedPlace = snapshot is not null
            ? First(snapshot.Vehicle.OwnerCitizenIdIssuedPlace, vehicle?.OwnerCitizenIdIssuedPlace, driver?.CitizenIdIssuedPlace, "Cục Cảnh Sát QLHC về TTXH")
            : First(vehicle?.OwnerCitizenIdIssuedPlace, driver?.CitizenIdIssuedPlace, "Cục Cảnh Sát QLHC về TTXH");
        var ownerAddress = snapshot is not null
            ? First(snapshot.Vehicle.OwnerAddress, vehicle?.OwnerAddress, driver?.Address, "...")
            : First(vehicle?.OwnerAddress, driver?.Address, "...");
        var ownerPhone = snapshot is not null
            ? First(snapshot.Vehicle.OwnerPhoneNumber, vehicle?.OwnerPhoneNumber, driver?.PhoneNumber, "...")
            : First(vehicle?.OwnerPhoneNumber, driver?.PhoneNumber, "...");

        var plateNumber = snapshot is not null
            ? First(snapshot.Vehicle.PlateNumber, contract.VehiclePlateSnapshot, vehicle?.PlateNumber, "...")
            : First(contract.VehiclePlateSnapshot, vehicle?.PlateNumber, "...");
        var brand = snapshot is not null
            ? First(snapshot.Vehicle.Brand, contract.VehicleBrandSnapshot, vehicle?.Brand, "...")
            : First(contract.VehicleBrandSnapshot, vehicle?.Brand, "...");
        var seatCount = (snapshot is not null
            ? snapshot.Vehicle.SeatCount ?? vehicle?.SeatCount
            : vehicle?.SeatCount)?.ToString(CultureInfo.InvariantCulture) ?? "...";
        var chassisNumber = snapshot is not null
            ? First(snapshot.Vehicle.ChassisNumber, vehicle?.ChassisNumber, "...")
            : First(vehicle?.ChassisNumber, "...");
        var engineNumber = snapshot is not null
            ? First(snapshot.Vehicle.EngineNumber, vehicle?.EngineNumber, "...")
            : First(vehicle?.EngineNumber, "...");
        var vehicleTypeDisplay = contract.BusinessType == ContractBusinessType.Cargo ? "XE TẢI" : "XE HỢP ĐỒNG";

        var subContractNumber = SubContractHelper.BuildSubContractNumber(
            contract.ContractNumber,
            contract.CreatedAt,
            rawCompanyName,
            rawCompanyTaxCode);
        var (effectiveFrom, effectiveTo) = SubContractHelper.GetEffectivePeriod(contract.CreatedAt);
        var contractDate = contract.CreatedAt.Kind == DateTimeKind.Utc
            ? contract.CreatedAt.AddHours(7)
            : contract.CreatedAt;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{{SUB_CONTRACT_NUMBER}}"] = subContractNumber,
            ["{{MAIN_CONTRACT_NUMBER}}"] = First(contract.ContractNumber, "..."),
            ["{{SIGN_DAY}}"] = contractDate.ToString("dd", CultureInfo.InvariantCulture),
            ["{{SIGN_MONTH}}"] = contractDate.ToString("MM", CultureInfo.InvariantCulture),
            ["{{SIGN_YEAR}}"] = contractDate.ToString("yyyy", CultureInfo.InvariantCulture),
            ["{{EFFECTIVE_FROM}}"] = effectiveFrom.ToString("dd / MM / yyyy", CultureInfo.InvariantCulture),
            ["{{EFFECTIVE_TO}}"] = effectiveTo.ToString("dd / MM / yyyy", CultureInfo.InvariantCulture),
            ["{{COMPANY_NAME}}"] = fixedUnit.PartyAName,
            ["{{COMPANY_ADDRESS}}"] = fixedUnit.Address,
            ["{{COMPANY_PHONE}}"] = fixedUnit.PhoneNumber,
            ["{{COMPANY_TAX_CODE}}"] = fixedUnit.TaxCode,
            ["{{COMPANY_LICENSE}}"] = fixedUnit.LicenseNumber,
            ["{{COMPANY_LICENSE_PLACE}}"] = fixedUnit.LicenseIssuedPlace,
            ["{{COMPANY_REPRESENTATIVE}}"] = fixedUnit.RepresentativeName,
            ["{{COMPANY_REP_POSITION}}"] = fixedUnit.RepresentativePosition,
            ["{{COMPANY_REP_CITIZEN_ID}}"] = fixedUnit.RepresentativeCitizenId,
            ["{{COMPANY_REP_ISSUED_DATE}}"] = fixedUnit.RepresentativeCitizenIdIssuedDateText,
            ["{{COMPANY_REP_ISSUED_PLACE}}"] = fixedUnit.RepresentativeCitizenIdIssuedPlace,
            ["{{OWNER_NAME}}"] = ownerName,
            ["{{OWNER_CITIZEN_ID}}"] = ownerCitizenId,
            ["{{OWNER_ISSUED_DATE}}"] = ownerIssuedDate,
            ["{{OWNER_ISSUED_PLACE}}"] = ownerIssuedPlace,
            ["{{OWNER_ADDRESS}}"] = ownerAddress,
            ["{{OWNER_PHONE}}"] = ownerPhone,
            ["{{VEHICLE_PLATE}}"] = plateNumber,
            ["{{VEHICLE_BRAND}}"] = brand,
            ["{{SEAT_COUNT}}"] = seatCount,
            ["{{VEHICLE_CHASSIS}}"] = chassisNumber,
            ["{{VEHICLE_ENGINE}}"] = engineNumber,
            ["{{VEHICLE_TYPE}}"] = vehicleTypeDisplay
        };
    }

    public static Dictionary<string, string> BuildSamplePlaceholders(CompanyProfile? company = null)
    {
        var map = SupportedPlaceholders.ToDictionary(
            x => x.Token,
            x => x.SampleValue,
            StringComparer.OrdinalIgnoreCase);

        if (company is not null)
        {
            var fixedUnit = SubContractHelper.ResolveFixedUnitProfile(
                company.CompanyName,
                company.TaxCode,
                fallbackAddress: company.Address,
                fallbackPhone: company.PhoneNumber,
                fallbackLicenseNumber: company.BusinessLicenseNumber,
                fallbackLicensePlace: company.BusinessLicenseIssuedPlace,
                fallbackRepName: company.RepresentativeName,
                fallbackRepPosition: company.RepresentativePosition,
                fallbackRepCitizenId: company.RepresentativeCitizenId,
                fallbackRepCitizenIssuedDate: company.RepresentativeCitizenIdIssuedDate,
                fallbackRepCitizenIssuedPlace: company.RepresentativeCitizenIdIssuedPlace);

            map["{{COMPANY_NAME}}"] = fixedUnit.PartyAName;
            map["{{COMPANY_ADDRESS}}"] = fixedUnit.Address;
            map["{{COMPANY_PHONE}}"] = fixedUnit.PhoneNumber;
            map["{{COMPANY_TAX_CODE}}"] = fixedUnit.TaxCode;
            map["{{COMPANY_LICENSE}}"] = fixedUnit.LicenseNumber;
            map["{{COMPANY_LICENSE_PLACE}}"] = fixedUnit.LicenseIssuedPlace;
            map["{{COMPANY_REPRESENTATIVE}}"] = fixedUnit.RepresentativeName;
            map["{{COMPANY_REP_POSITION}}"] = fixedUnit.RepresentativePosition;
            map["{{COMPANY_REP_CITIZEN_ID}}"] = fixedUnit.RepresentativeCitizenId;
            map["{{COMPANY_REP_ISSUED_DATE}}"] = fixedUnit.RepresentativeCitizenIdIssuedDateText;
            map["{{COMPANY_REP_ISSUED_PLACE}}"] = fixedUnit.RepresentativeCitizenIdIssuedPlace;
            map["{{SUB_CONTRACT_NUMBER}}"] = SubContractHelper.BuildSubContractNumber(
                fixedUnit.IsCaoSonPreset ? "65E00389-02" : "83G00100-04",
                DateTime.UtcNow,
                company.CompanyName,
                company.TaxCode);

            if (!fixedUnit.IsCaoSonPreset && SubContractHelper.IsMekongXanhUnit(company.CompanyName, company.TaxCode))
            {
                map["{{OWNER_NAME}}"] = "ĐẶNG MINH HIỀN";
                map["{{OWNER_CITIZEN_ID}}"] = "94091006036";
                map["{{OWNER_ISSUED_DATE}}"] = "09/08/2021";
                map["{{OWNER_ISSUED_PLACE}}"] = "Cục Cảnh Sát QLHC về TTXH";
                map["{{OWNER_ADDRESS}}"] = "Xây Đá B, Xã Hồ Đác Kiệm, Huyện Châu Thành, Tỉnh Sóc Trăng";
                map["{{VEHICLE_PLATE}}"] = "83G-001.00";
                map["{{VEHICLE_BRAND}}"] = "VINFAST";
                map["{{SEAT_COUNT}}"] = "5";
                map["{{VEHICLE_CHASSIS}}"] = "RLLVAGND4RH770763";
                map["{{VEHICLE_ENGINE}}"] = "VFCAJB24B180043";
            }
        }

        return map;
    }

    public static string ReplacePlaceholders(
        string? templateText,
        IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrWhiteSpace(templateText))
            return string.Empty;

        var result = templateText;
        foreach (var kvp in values)
        {
            result = result.Replace(kvp.Key, kvp.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private SubContractTemplateBundleDto LoadBundle()
    {
        var path = GetTemplatesJsonPath();
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<SubContractTemplateBundleDto>(json, ReadJsonOptions);
                if (loaded is not null)
                {
                    loaded.DefaultCaoSon ??= CreateDefaultPreset(isCaoSon: true);
                    loaded.DefaultMekongXanh ??= CreateDefaultPreset(isCaoSon: false);
                    loaded.CompanyTemplates ??= new(StringComparer.OrdinalIgnoreCase);
                    return loaded;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không thể đọc file SubContractTemplates.json, sử dụng mẫu mặc định.");
            }
        }

        return new SubContractTemplateBundleDto
        {
            Version = 2,
            DefaultCaoSon = CreateDefaultPreset(isCaoSon: true),
            DefaultMekongXanh = CreateDefaultPreset(isCaoSon: false),
            CompanyTemplates = new(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static async Task PersistBundleAsync(
        string path,
        SubContractTemplateBundleDto bundle,
        CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);

        if (File.Exists(path))
        {
            var backupPath = Path.Combine(dir, $"{Path.GetFileName(path)}.bak-{DateTime.Now:yyyyMMddHHmmssfff}");
            File.Copy(path, backupPath, overwrite: true);
        }

        var json = JsonSerializer.Serialize(bundle, WriteJsonOptions);
        var tempPath = Path.Combine(dir, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(tempPath, json, Encoding.UTF8, ct);
        File.Move(tempPath, path, overwrite: true);
    }

    /// <summary>
    /// Tạo mẫu Hợp đồng phụ cứng riêng cho từng đơn vị cụ thể.
    /// </summary>
    public static SubContractTemplateDto CreateFixedUnitTemplate(
        CompanyProfile? company,
        Guid? companyProfileId,
        string? companyName,
        string? taxCode)
    {
        if (SubContractHelper.IsCaoSonUnit(companyName ?? company?.CompanyName, taxCode ?? company?.TaxCode))
        {
            var caoSon = CreateDefaultPreset(isCaoSon: true);
            caoSon.CompanyProfileId = companyProfileId ?? company?.Id;
            return caoSon;
        }

        if (SubContractHelper.IsMekongXanhUnit(companyName ?? company?.CompanyName, taxCode ?? company?.TaxCode))
        {
            var mekong = CreateDefaultPreset(isCaoSon: false);
            mekong.CompanyProfileId = companyProfileId ?? company?.Id;
            return mekong;
        }

        var fixedUnit = SubContractHelper.ResolveFixedUnitProfile(
            companyName ?? company?.CompanyName,
            taxCode ?? company?.TaxCode,
            fallbackAddress: company?.Address,
            fallbackPhone: company?.PhoneNumber,
            fallbackLicenseNumber: company?.BusinessLicenseNumber,
            fallbackLicensePlace: company?.BusinessLicenseIssuedPlace,
            fallbackRepName: company?.RepresentativeName,
            fallbackRepPosition: company?.RepresentativePosition,
            fallbackRepCitizenId: company?.RepresentativeCitizenId,
            fallbackRepCitizenIssuedDate: company?.RepresentativeCitizenIdIssuedDate,
            fallbackRepCitizenIssuedPlace: company?.RepresentativeCitizenIdIssuedPlace);

        var template = CreateDefaultPreset(isCaoSon: false);
        template.CompanyProfileId = companyProfileId ?? company?.Id;
        template.TemplateName = $"Mẫu HĐ phụ cứng — {fixedUnit.PartyAName}";
        template.UnitHeaderTitle = fixedUnit.UnitHeaderTitle;
        template.IntroLocationText = string.Join("\n",
        [
            "Hôm nay, ngày {{SIGN_DAY}} tháng {{SIGN_MONTH}} năm {{SIGN_YEAR}}",
            $"Tại văn phòng {fixedUnit.OfficeLocationName} chúng tôi gồm:"
        ]);
        template.PartyAText = string.Join("\n",
        [
            fixedUnit.PartyAName,
            $"Địa chỉ trụ sở: {fixedUnit.Address}   -   Số điện thoại (Fax): {fixedUnit.PhoneNumber}",
            $"{fixedUnit.LicenseLabel} : {fixedUnit.LicenseNumber}   -   Nơi cấp: {fixedUnit.LicenseIssuedPlace}   -   Mã số thuế : {fixedUnit.TaxCode}",
            $"Đại diện: {fixedUnit.RepresentativeName}   -   Chức Vụ: {fixedUnit.RepresentativePosition}",
            $"CCCD số: {fixedUnit.RepresentativeCitizenId}   -   Ngày cấp: {fixedUnit.RepresentativeCitizenIdIssuedDateText}   -   Nơi cấp: {fixedUnit.RepresentativeCitizenIdIssuedPlace}"
        ]);
        template.SignatureLeftSignerName = fixedUnit.SignatureRepresentativeName;
        return template;
    }

    /// <summary>
    /// Tạo 2 mẫu Hợp đồng phụ cứng chuẩn 100% theo 2 khu HTX:
    /// - Khu CN HTX Công nghệ VT Cao Sơn Car tại Cần Thơ (11 điều khoản)
    /// - Khu Chi nhánh HTX Cộng đồng Mekong Xanh Cần Thơ (10 điều khoản)
    /// </summary>
    public static SubContractTemplateDto CreateDefaultPreset(bool isCaoSon)
    {
        var unitHeader = isCaoSon
            ? "CN HTX CÔNG NGHỆ VT CAO SƠN CAR TẠI CẦN THƠ"
            : "CHI NHÁNH HỢP TÁC XÃ CỘNG ĐỒNG MEKONG XANH CẦN THƠ";

        var introLocation = isCaoSon
            ? """
              Hôm nay, ngày {{SIGN_DAY}} tháng {{SIGN_MONTH}} năm {{SIGN_YEAR}}
              Tại văn phòng CN Hợp tác xã công nghệ vận tải Cao Sơn Car tại Cần Thơ chúng tôi gồm:
              """
            : """
              Hôm nay, ngày {{SIGN_DAY}} tháng {{SIGN_MONTH}} năm {{SIGN_YEAR}}
              Tại văn phòng CHI NHÁNH HỢP TÁC XÃ CỘNG ĐỒNG MEKONG XANH CẦN THƠ chúng tôi gồm:
              """;

        var partyAText = isCaoSon
            ? """
              CN HTX CÔNG NGHỆ VT CAO SƠN CAR TẠI CẦN THƠ
              Địa chỉ trụ sở: Số 47 đường số 12, KTĐC Thới Nhựt 2, Phường Tân An, Thành phố Cần Thơ   -   Số điện thoại (Fax): 0925351111
              Giấy chứng nhận đăng ký kinh doanh số : 922643479/GPKDVT   -   Ngày cấp: 07 tháng 08 năm 2026   -   Nơi cấp: Sở Xây dựng Thành phố Cần Thơ   -   Mã số thuế : 0319340385-001
              Đại diện: Nguyễn Trọng Hậu   -   Chức Vụ: Giám đốc.
              CCCD số: 82203018675   -   Ngày cấp: 19/01/2026   -   Nơi cấp: Bộ Công An
              """
            : """
              CHI NHÁNH HỢP TÁC XÃ CỘNG ĐỒNG MEKONG XANH CẦN THƠ
              Địa chỉ trụ sở: Số 4/48 Đường Cách Mạng Tháng 8, Phường Cái Khế, Thành Phố Cần Thơ   -   Số điện thoại (Fax): 0911772265
              Giấy phép kinh doanh vận tải số : 922641667/GPKDVT   -   Ngày cấp: 16 tháng 07 năm 2026   -   Nơi cấp: Sở Xây dựng Thành phố Cần Thơ   -   Mã số thuế : 0319595866-001
              Đại diện: Phạm Hoàng Nam   -   Chức Vụ: Giám đốc
              CCCD số: 091092007059   -   Ngày cấp: 22/11/2022   -   Nơi cấp: Cục Cảnh Sát QLHC về TTXH
              """;

        var article3PartyA = isCaoSon
            ? """
              Trách nhiệm:
              - Bên A có trách nhiệm hỗ trợ cung ứng nguồn khách, nguồn hàng… cho thành viên ( nếu có ).
              - Thông báo kịp thời cho Bên B về nguồn khách, nguồn hàng… nếu có sự thay đổi về thời gian, lộ trình, giá cả và các yêu cầu khác ( nếu có ).
              Quyền hạn:
              - Hợp Tác Xã được quyền, trách nhiệm và nghĩa vụ quản lý, sử dụng và điều hành phương tiện theo hợp đồng này ( bao gồm chi phí nhiên liệu, sửa chữa, BOT, ... ) khi phương tiện có phát sinh doanh thu tại HTX.
              - Yêu cầu thành viên cung cấp thông tin và những điều kiện cần thiết khác để thực hiện tốt công việc cung cấp sản phẩm, dịch vụ vận tải của Hợp Tác Xã.
              - Yêu cầu thành viên thanh toán các loại thuế ( nếu có ) và phí dịch vụ cho HTX đúng hạn.
              - Đối với các xã viên không có phát sinh doanh thu tại app Grab và app Xanh Plasform thì phí duy trì HTX: 500.000 VND/năm. Phần phí này sẽ được thu cùng lúc cho các năm của phù hiệu được cấp trên 1 xe khi làm hợp đồng thanh lý.
              - Từ chối cung cấp sản phẩm, dịch vụ về pháp lý khi thành viên vi phạm thỏa thuận hợp đồng, vi phạm nội quy của HTX và các điều khoản quy định của ngành vận tải, hoặc vi phạm pháp luật.
              - HTX có quyền đề nghị cấp thời hạn có hiệu lực của phù hiệu, biển hiệu nhưng không quá thời hạn có hiệu lực của Giấy phép kinh doanh vận tải và không quá niên hạn sử dụng của phương tiện.
              - Các loại phù hiệu, biển hiệu do HTX cấp cho thành viên là tài sản của HTX. Bên A có quyền ngưng cấp "LỆNH VẬN CHUYỂN"; thu hồi Phù hiệu "XE HỢP ĐỒNG"; "XE TẢI"… của bên B trong trường hợp xe cố tình vi phạm luật GTĐB, vi phạm pháp luật hoặc không đóng các khoản phí cho HTX đúng hạn.
              """
            : """
              Trách nhiệm:
              - Bên A có trách nhiệm hỗ trợ cung ứng nguồn khách, nguồn hàng… cho thành viên ( nếu có ).
              - Thông báo kịp thời cho Bên B về nguồn khách, nguồn hàng… nếu có sự thay đổi về thời gian, lộ trình, giá cả và các yêu cầu khác ( nếu có ).
              Quyền hạn:
              - Hợp Tác Xã được quyền, trách nhiệm và nghĩa vụ quản lý, sử dụng và điều hành phương tiện theo hợp đồng này ( bao gồm chi phí nhiên liệu, sửa chữa, BOT, ... ) khi phương tiện có phát sinh doanh thu tại HTX.
              - Yêu cầu thành viên cung cấp thông tin và những điều kiện cần thiết khác để thực hiện tốt công việc cung cấp sản phẩm, dịch vụ vận tải của Hợp Tác Xã.
              - Từ chối cung cấp sản phẩm, dịch vụ về pháp lý khi thành viên vi phạm thỏa thuận hợp đồng, vi phạm nội quy của HTX và các điều khoản quy định của ngành vận tải, hoặc vi phạm pháp luật.
              - HTX có quyền đề nghị cấp thời hạn có hiệu lực của phù hiệu, biển hiệu nhưng không quá thời hạn có hiệu lực của Giấy phép kinh doanh vận tải và không quá niên hạn sử dụng của phương tiện.
              - Các loại phù hiệu, biển hiệu do HTX cấp cho thành viên là tài sản của HTX. Bên A có quyền ngưng cấp "LỆNH VẬN CHUYỂN"; thu hồi Phù hiệu "XE HỢP ĐỒNG"; "XE TẢI"… của bên B trong trường hợp xe cố tình vi phạm luật GTĐB, vi phạm pháp luật hoặc không đóng các khoản phí cho HTX đúng hạn.
              """;

        return new SubContractTemplateDto
        {
            TemplateName = isCaoSon
                ? "Mẫu HĐ phụ cứng — Khu CN HTX Công nghệ VT Cao Sơn Car tại Cần Thơ"
                : "Mẫu HĐ phụ cứng — Khu Chi nhánh HTX Cộng đồng Mekong Xanh Cần Thơ",
            UnitHeaderTitle = unitHeader,
            ContractNumberLine = "Số:{{SUB_CONTRACT_NUMBER}}",
            DocumentTitle = "HỢP ĐỒNG DỊCH VỤ VẬN TẢI",
            LegalBasesText = """
                Căn cứ Nghị định số 158/2024/NĐ-CP ngày 18 tháng 12 năm 2024 của Chính phủ Quy định về hoạt động vận tải đường bộ;
                Căn cứ Nghị định số 218/2026/NĐ-CP ngày 19 tháng 06 năm 2026 của Chính phủ về sửa đổi, bổ sung một số điều của Nghị định 158/2024/NĐ-CP ngày 18 tháng 12 năm 2024 của Chính phủ Quy định về hoạt động vận tải đường bộ;
                Căn cứ Thông tư 36/2024/TT-BGTVT ngày 15 tháng 11 năm 2024 của Bộ Giao thông vận tải Quy định về tổ chức, quản lý hoạt động vận tải bằng xe ô tô và hoạt động của bến xe, bãi đỗ xe, trạm dừng nghỉ, điểm dừng xe trên đường bộ; quy định trình tự, thủ tục đưa bến xe, trạm dừng nghỉ vào khai thác;
                Căn cứ Thông tư số 72/2025/TT-BXD ngày 31 tháng 12 năm 2025 của Bộ trưởng Bộ xây dựng về sửa bổ sung một số điều của Thông tư số 36/2024/TT-BGTVT ngày 15 tháng 11 năm 2024;
                """,
            IntroLocationText = introLocation,
            PartyAText = partyAText,
            PartyBText = """
                Họ tên / Chủ phương tiện: {{OWNER_NAME}}
                CCCD số: {{OWNER_CITIZEN_ID}}   -   Ngày cấp: {{OWNER_ISSUED_DATE}}   -   Nơi cấp: {{OWNER_ISSUED_PLACE}}
                Địa chỉ : {{OWNER_ADDRESS}}
                Chủ phương tiện biển số: {{VEHICLE_PLATE}}   -   Nhãn hiệu: {{VEHICLE_BRAND}}   -   Số chỗ: {{SEAT_COUNT}}
                Số Khung: {{VEHICLE_CHASSIS}}   -   Số Máy: {{VEHICLE_ENGINE}}
                Nơi sản xuất : VIỆT NAM   -   Tải trọng (kg): /   -   Loại phương tiện: {{VEHICLE_TYPE}}
                """,
            PreambleText = "Sau khi trao đổi, bàn bạc về yêu cầu sử dụng dịch vụ vận tải của bên B và khả năng cung ứng dịch vụ vận tải của bên A. Hai bên thống nhất ký hợp đồng với những điều khoản sau đây:",
            Article1Text = """
                Bên A đồng ý cung ứng cho bên B các sản phẩm dịch vụ vận tải sau:
                - Dịch vụ vận tải về hành chính: Dịch vụ hỗ trợ các thủ tục về hành chính do ngành Giao Thông vận tải quy định.
                - Dịch vụ vận chuyển hành khách theo Hợp Đồng, vận chuyển hành khách Du Lịch.
                - Dịch vụ về nguồn hàng hóa, nguồn hành khách.
                - Dịch vụ hỗ trợ thành viên mua xe, thay đổi phương tiện vận chuyển.
                - Dịch vụ hỗ trợ thành viên mua bảo hiểm bắt buộc, bảo hiểm vật chất tự nguyện.
                - Dịch vụ hỗ trợ sửa chữa phương tiện vận tải.
                - Dịch vụ khác theo yêu cầu của thành viên HTX.
                """,
            Article2Text = """
                - Chi phí phải trả cho từng sản phẩm dịch vụ vận tải cụ thể từng năm.
                - Phương thức thanh toán: Thanh toán 100% bằng tiền mặt hoặc chuyển khoản.
                """,
            Article3PartyAText = article3PartyA,
            Article3PartyBText = """
                Trách nhiệm:
                - Cam kết cung cấp thông tin trung thực, cần thiết để HTX cung ứng sản phẩm, dịch vụ VT thuận lợi; tuân thủ theo Luật pháp của Việt Nam và điều lệ, quy chế của HTX.
                - Không được chở hành khách quá quy định và hàng hoá trái phép; thanh toán các khoản thuế của thành viên HTX phải đóng khi cơ quan thuế yêu cầu; thanh toán phí sử dụng sản phẩm dịch vụ vận tải đúng hạn.
                - Thông báo cho bên A ngay khi có thay đổi; luôn kiểm tra kỹ thuật xe và lái xe an toàn.
                - Lái xe phải nghiêm chỉnh chấp hành Luật Giao thông đường bộ Việt Nam khi tham gia giao thông. Trong trường hợp lái xe bị xử phạt về hành vi vi phạm An toàn giao thông đường bộ Việt Nam thì lái xe phải hoàn toàn chịu trách nhiệm xử phạt trước Pháp luật về các hành vi trên và Bên A sẽ không phải chịu trách nhiệm liên đới.
                Quyền hạn:
                - Yêu cầu bên cung ứng sản phẩm dịch vụ vận tải HTX thực hiện công việc theo đúng chất lượng, số lượng, thời gian, địa điểm và các thỏa thuận khác.
                - Được đàm phán khi có sự thay đổi về các sản phẩm, dịch vụ vận tải được cung ứng làm ảnh hưởng đến sản xuất kinh doanh của thành viên.
                - Trong trường hợp bên A cung ứng dịch vụ vận tải vi phạm nghiêm trọng nghĩa vụ thì bên B có quyền đơn phương chấm dứt thực hiện hợp đồng và yêu cầu bồi thường thiệt hại; ngưng sử dụng sản phẩm dịch vụ vận tải khi các yêu cầu không được HTX giải quyết, khắc phục.
                """,
            Article4Text = """
                - Thành viên không sử dụng sản phẩm dịch vụ trong thời gian liên tục 6 tháng thì sẽ bị chấm dứt tư cách thành viên và tự chịu mọi trách nhiệm.
                - Trong 45 ngày kể từ ngày có phù hiệu nếu xã viên không cung cấp đủ hồ sơ thì sẽ thu hồi phù hiệu do thiếu hồ sơ lưu trữ.
                - Các trường hợp lái xe không hoạt động tại khu vực Cần Thơ sẽ áp dụng mức phí quản lý là 300.000 VNĐ/tháng. Các chi phí quản lý có thể thay đổi theo tình hình và điều kiện kinh doanh.
                - Ngoài các chi phí cố định nêu trên, các chi phí quản lý và vận hành khác có thể được điều chỉnh, phát sinh hoặc thay đổi tùy theo tình hình thực tế, nhu cầu hoạt động và điều kiện kinh doanh của công ty tại từng thời điểm nhằm đảm bảo hiệu quả quản lý và vận hành và đúng pháp luật.
                - Tài xế/chủ xe có trách nhiệm cung cấp đầy đủ và chính xác các thông tin theo đúng nội dung hợp đồng, đồng thời đảm bảo cung cấp thông tin và dữ liệu định vị của chuyến đi khi có yêu cầu của HTX. Về thực hiện việc xuất hóa đơn Tài xế/chủ xe cam kết không được xuất hóa đơn khống, mua bán hóa đơn.
                """,
            Article5Text = """
                - Hợp đồng có hiệu lực 4 năm kể từ ngày ký: {{EFFECTIVE_FROM}} đến {{EFFECTIVE_TO}} hoặc đến khi hai bên ký thanh lý hợp đồng.
                - Khi bên B thanh lý trước thời hạn, phí rời HTX: 2.000.000 VNĐ, thời gian thanh lý trong 15 ngày (tính các ngày làm việc), kể từ khi bên B yêu cầu thanh lý và hoàn trả phù hiệu cho HTX.
                - Các nảy sinh tranh chấp được giải quyết trên cơ sở hòa giải trên tinh thần hợp tác, tương trợ lẫn nhau; trường hợp không đạt được thỏa thuận thì trình Đại hội thành viên xem xét giải quyết. Trường hợp không giải quyết được thì một trong hai bên có quyền yêu cầu cơ quan nhà nước có thẩm quyền xử lý theo quy định của pháp luật.
                - Hợp đồng này được làm thành 03 bản có giá trị như nhau. Bên A giữ 01 bản, bên B giữ 01 bản và 01 bản nộp Sở Xây Dựng.
                """,
            SignatureLeftTitle = "ĐẠI DIỆN BÊN A",
            SignatureLeftSignerName = isCaoSon ? "NGUYỄN TRỌNG HẬU" : "PHẠM HOÀNG NAM",
            SignatureRightTitle = "ĐẠI DIỆN BÊN B"
        };
    }

    private static void ParseParagraphsIntoTemplate(IReadOnlyList<string> paragraphs, SubContractTemplateDto target)
    {
        var legalLines = new List<string>();
        var partyALines = new List<string>();
        var a1Lines = new List<string>();
        var a2Lines = new List<string>();
        var a3ALines = new List<string>();
        var a3BLines = new List<string>();
        var a4Lines = new List<string>();
        var a5Lines = new List<string>();

        var section = "header";
        foreach (var raw in paragraphs)
        {
            var line = raw.Trim();
            if (line.StartsWith("Căn cứ ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Căncứ", StringComparison.OrdinalIgnoreCase))
            {
                legalLines.Add(line);
                continue;
            }

            if (line.StartsWith("Bên A", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("BênA", StringComparison.OrdinalIgnoreCase))
            {
                section = "partya";
                continue;
            }
            if (line.StartsWith("Bên B", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("BênB", StringComparison.OrdinalIgnoreCase))
            {
                section = "partyb";
                continue;
            }
            if (line.StartsWith("Điều 1", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Điều1", StringComparison.OrdinalIgnoreCase))
            {
                section = "a1";
                continue;
            }
            if (line.StartsWith("Điều 2", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Điều2", StringComparison.OrdinalIgnoreCase))
            {
                section = "a2";
                continue;
            }
            if (line.StartsWith("Điều 3", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Điều3", StringComparison.OrdinalIgnoreCase))
            {
                section = "a3a";
                continue;
            }
            if (line.Contains("Trách nhiệm và quyền hạn của bên B", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("TráchnhiệmvàquyềnhạncủabênB", StringComparison.OrdinalIgnoreCase))
            {
                section = "a3b";
                continue;
            }
            if (line.StartsWith("Điều 4", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Điều4", StringComparison.OrdinalIgnoreCase))
            {
                section = "a4";
                continue;
            }
            if (line.StartsWith("Điều 5", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Điều5", StringComparison.OrdinalIgnoreCase))
            {
                section = "a5";
                continue;
            }
            if (line.Contains("ĐẠI DIỆN BÊN A", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("ĐẠIDIỆNBÊN", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            switch (section)
            {
                case "partya":
                    partyALines.Add(line);
                    break;
                case "a1":
                    a1Lines.Add(line);
                    break;
                case "a2":
                    a2Lines.Add(line);
                    break;
                case "a3a":
                    if (!line.Contains("Trách nhiệm và quyền hạn của bên A", StringComparison.OrdinalIgnoreCase) &&
                        !line.Contains("TráchnhiệmvàquyềnhạncủabênA", StringComparison.OrdinalIgnoreCase))
                        a3ALines.Add(line);
                    break;
                case "a3b":
                    a3BLines.Add(line);
                    break;
                case "a4":
                    a4Lines.Add(line);
                    break;
                case "a5":
                    a5Lines.Add(line);
                    break;
            }
        }

        if (legalLines.Count > 0)
            target.LegalBasesText = string.Join(Environment.NewLine, legalLines);
        if (partyALines.Count > 0)
            target.PartyAText = string.Join(Environment.NewLine, partyALines);
        if (a1Lines.Count > 0)
            target.Article1Text = string.Join(Environment.NewLine, a1Lines);
        if (a2Lines.Count > 0)
            target.Article2Text = string.Join(Environment.NewLine, a2Lines);
        if (a3ALines.Count > 0)
            target.Article3PartyAText = string.Join(Environment.NewLine, a3ALines);
        if (a3BLines.Count > 0)
            target.Article3PartyBText = string.Join(Environment.NewLine, a3BLines);
        if (a4Lines.Count > 0)
            target.Article4Text = string.Join(Environment.NewLine, a4Lines);
        if (a5Lines.Count > 0)
            target.Article5Text = string.Join(Environment.NewLine, a5Lines);
    }

    private static void AppendMultiLineBlock(Body body, string text, bool bold, bool italic)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var lines = text.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var isSubHeading = trimmed.Equals("Trách nhiệm:", StringComparison.OrdinalIgnoreCase) ||
                               trimmed.Equals("Quyền hạn:", StringComparison.OrdinalIgnoreCase);
            AppendParagraph(body, trimmed, bold: bold, italic: italic || isSubHeading);
        }
    }

    private static void AppendParagraph(
        Body body,
        string text,
        bool bold,
        bool italic,
        bool center = false,
        string fontSizeHalfPt = "23")
    {
        var runProps = new RunProperties(
            new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", ComplexScript = "Times New Roman" },
            new FontSize { Val = fontSizeHalfPt },
            new FontSizeComplexScript { Val = fontSizeHalfPt });

        if (bold)
            runProps.Append(new Bold());
        if (italic)
            runProps.Append(new Italic());

        var paraProps = new ParagraphProperties(
            new SpacingBetweenLines { After = "80", Line = "276", LineRule = LineSpacingRuleValues.Auto },
            new Justification { Val = center ? JustificationValues.Center : JustificationValues.Both });

        var paragraph = new Paragraph(
            paraProps,
            new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

        body.Append(paragraph);
    }

    private static TableCell CreateTableCell(IEnumerable<(string Text, bool Bold, bool Italic, string FontSizeHalfPt)> lines)
    {
        var cell = new TableCell(
            new TableCellProperties(
                new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "2500" }));

        foreach (var (text, bold, italic, fontSize) in lines)
        {
            var runProps = new RunProperties(
                new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", ComplexScript = "Times New Roman" },
                new FontSize { Val = fontSize },
                new FontSizeComplexScript { Val = fontSize });

            if (bold)
                runProps.Append(new Bold());
            if (italic)
                runProps.Append(new Italic());

            var paraProps = new ParagraphProperties(
                new SpacingBetweenLines { After = "40", Line = "240", LineRule = LineSpacingRuleValues.Auto },
                new Justification { Val = JustificationValues.Center });

            cell.Append(new Paragraph(
                paraProps,
                new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve })));
        }

        return cell;
    }

    private static async Task EnsureOwnerAccessAsync(AuthenticationStateProvider authStateProvider)
    {
        var auth = await authStateProvider.GetAuthenticationStateAsync();
        var user = auth.User;
        if (user.Identity?.IsAuthenticated == true && user.IsInRole("Owner"))
            return;

        throw new UnauthorizedAccessException("Chỉ tài khoản Owner mới có quyền chỉnh sửa Mẫu Hợp đồng phụ.");
    }

    private static SubContractTemplateDto Clone(SubContractTemplateDto source)
        => JsonSerializer.Deserialize<SubContractTemplateDto>(
            JsonSerializer.Serialize(source, WriteJsonOptions),
            ReadJsonOptions)!;

    private static string FormatDateOnly(DateTime? value)
        => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "...";

    private static string First(params string?[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
}
