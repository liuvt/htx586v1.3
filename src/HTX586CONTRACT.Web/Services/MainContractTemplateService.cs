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
using HTX586CONTRACT.Domain.Signatures;
using Microsoft.AspNetCore.Components.Authorization;

namespace HTX586CONTRACT.Web.Services;

public sealed class MainContractPlaceholderInfo
{
    public string Token { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string SampleValue { get; init; } = string.Empty;
    public string Category { get; init; } = "Chung";
}

public sealed class MainContractTemplateDto
{
    public Guid? CompanyProfileId { get; set; }
    public ContractBusinessType BusinessType { get; set; } = ContractBusinessType.Passenger;
    public string TemplateName { get; set; } = "Mẫu Hợp đồng vận chuyển hành khách";

    // Trang 1 - Quốc hiệu & Tiêu đề
    public string LeftHeaderLine1 { get; set; } = string.Empty;
    public string LeftHeaderLine2 { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = "HỢP ĐỒNG VẬN CHUYỂN HÀNH KHÁCH";
    public string ContractNumberLine { get; set; } = "Số: {{CONTRACT_NUMBER}}/HĐVC-HTX";
    public string LegalBasesText { get; set; } = string.Empty;
    public string IntroDateLocationText { get; set; } = string.Empty;

    // Trang 1 - Bên A & Bên B
    public string PartyASectionTitle { get; set; } = "I- ĐẠI DIỆN BÊN A: (Bên vận tải)";
    public string PartyABodyText { get; set; } = string.Empty;
    public string PartyBSectionTitle { get; set; } = "II- ĐẠI DIỆN BÊN B: (Bên thuê vận tải)";
    public string PartyBBodyText { get; set; } = string.Empty;

    // Trang 1 - Nội dung vận chuyển & Thanh toán
    public string TransportDetailsTitle { get; set; } = "Hai bên cùng thống nhất ký hợp đồng vận chuyển khách cụ thể như sau:";
    public string TransportDetailsBodyText { get; set; } = string.Empty;

    // Trang 1 - III. Trách nhiệm các bên
    public string ResponsibilitiesSectionTitle { get; set; } = "III- TRÁCH NHIỆM CÁC BÊN";
    public string ResponsibilityHtxTitle { get; set; } = "BÊN HTX";
    public string ResponsibilityHtxBodyText { get; set; } = string.Empty;
    public string ResponsibilityOwnerTitle { get; set; } = "BÊN CHỦ XE";
    public string ResponsibilityOwnerBodyText { get; set; } = string.Empty;
    public string ResponsibilityCustomerTitle { get; set; } = "BÊN B";
    public string ResponsibilityCustomerBodyText { get; set; } = string.Empty;
    public string GeneralCommitmentText { get; set; } = string.Empty;

    // Trang 1 - Chữ ký 3 cột
    public string Page1SigLeftTitle { get; set; } = "ĐẠI DIỆN HTX";
    public string Page1SigCenterTitle { get; set; } = "CHỦ XE";
    public string Page1SigRightTitle { get; set; } = "ĐẠI DIỆN BÊN B";

    // Trang 2 - Danh sách hành khách (Passenger) hoặc Giấy vận tải (Cargo)
    public string Page2LeftHeaderLine1 { get; set; } = "{{HEADER_COMPANY_OFFICE_NAME}}";
    public string Page2LeftHeaderLine2 { get; set; } = string.Empty;
    public string Page2Title { get; set; } = "DANH SÁCH HÀNH KHÁCH";
    public string Page2Subtitle { get; set; } = "(Kèm theo hợp đồng vận chuyển số: {{CONTRACT_NUMBER}}/HĐVC-HTX ngày {{CONTRACT_DAY}} tháng {{CONTRACT_MONTH}} năm {{CONTRACT_YEAR}})";
    public string Page2FooterSummaryLine { get; set; } = "TỔNG SỐ: {{PASSENGER_COUNT_2D}} Người";
    public string Page2SigLeftTitle { get; set; } = "BÊN B";
    public string Page2SigLeftSubTitle { get; set; } = "Ký tên, đóng dấu (nếu có)";
    public string Page2SigRightTitle { get; set; } = "TÀI XẾ";
    public string Page2SigRightSubTitle { get; set; } = "Ký và ghi rõ họ tên";

    public string? UploadedDocxFileName { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class MainContractTemplateBundleDto
{
    public int Version { get; set; } = 1;
    public MainContractTemplateDto DefaultPassenger { get; set; } = new();
    public MainContractTemplateDto DefaultCargo { get; set; } = new();
    public Dictionary<string, MainContractTemplateDto> CompanyPassengerTemplates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, MainContractTemplateDto> CompanyCargoTemplates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Quản lý Mẫu Hợp đồng Chính (HĐ Vận chuyển Hành khách 2 trang &amp; HĐ Vận chuyển Hàng hóa 2 trang)
/// được convert chuẩn xác từ 2 file PDF gốc (HopDongVanChuyenHanhKhach.template.pdf &amp; HopDongVanChuyenHangHoa.template.pdf).
/// Hỗ trợ chỉnh sửa trực tiếp trên Web (Hướng 1) và Tải về / Upload file Word .docx 2 trang (Hướng 3).
/// Chỉ Owner mới có quyền chỉnh sửa.
/// </summary>
public sealed class MainContractTemplateService(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILogger<MainContractTemplateService> logger)
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

    public static IReadOnlyList<MainContractPlaceholderInfo> SupportedPlaceholders { get; } =
    [
        new() { Token = "{{CONTRACT_NUMBER}}", Label = "Số hợp đồng chính", SampleValue = "792637383-01", Category = "Hợp đồng" },
        new() { Token = "{{CONTRACT_TIME}}", Label = "Giờ lập HĐ (HH:mm)", SampleValue = "08:30", Category = "Hợp đồng" },
        new() { Token = "{{CONTRACT_DAY}}", Label = "Ngày lập HĐ (dd)", SampleValue = "28", Category = "Hợp đồng" },
        new() { Token = "{{CONTRACT_MONTH}}", Label = "Tháng lập HĐ (MM)", SampleValue = "11", Category = "Hợp đồng" },
        new() { Token = "{{CONTRACT_YEAR}}", Label = "Năm lập HĐ (yyyy)", SampleValue = "2026", Category = "Hợp đồng" },
        new() { Token = "{{COMPANY_NAME}}", Label = "Tên Doanh nghiệp / HTX", SampleValue = "HỢP TÁC XÃ CÔNG NGHỆ VẬN TẢI CAO SƠN CAR", Category = "Bên vận tải" },
        new() { Token = "{{COMPANY_OFFICE_NAME}}", Label = "Tên Văn phòng lập HĐ", SampleValue = "HỢP TÁC XÃ CÔNG NGHỆ VẬN TẢI CAO SƠN CAR", Category = "Bên vận tải" },
        new() { Token = "{{COMPANY_TAX_CODE}}", Label = "Mã số thuế HTX", SampleValue = "0319340385", Category = "Bên vận tải" },
        new() { Token = "{{COMPANY_LICENSE}}", Label = "Số GPKDVT", SampleValue = "792637383/GPKDVT", Category = "Bên vận tải" },
        new() { Token = "{{COMPANY_LICENSE_ISSUED_PLACE}}", Label = "Cơ quan cấp GPKDVT", SampleValue = "Sở Giao Thông Vận Tải Cần Thơ", Category = "Bên vận tải" },
        new() { Token = "{{COMPANY_ADDRESS}}", Label = "Địa chỉ trụ sở HTX", SampleValue = "25 Đường số 7, KDC Cityland Center Hills - P.Hạnh Thông - TP.Hồ Chí Minh", Category = "Bên vận tải" },
        new() { Token = "{{COMPANY_PHONE}}", Label = "Điện thoại HTX", SampleValue = "0925351111", Category = "Bên vận tải" },
        new() { Token = "{{COMPANY_REPRESENTATIVE}}", Label = "Người đại diện HTX", SampleValue = "NGUYỄN TRỌNG HẬU", Category = "Bên vận tải" },
        new() { Token = "{{COMPANY_REP_CITIZEN_ID}}", Label = "CCCD đại diện HTX", SampleValue = "082203018675", Category = "Bên vận tải" },
        new() { Token = "{{COMPANY_REP_ISSUED_DATE}}", Label = "Ngày cấp CCCD đại diện HTX", SampleValue = "19/01/2026", Category = "Bên vận tải" },
        new() { Token = "{{COMPANY_COMPLAINT_CONTACT}}", Label = "Liên hệ phản ánh", SampleValue = "Sở GTVT Cần Thơ: 0939.984.333 - 0907.877.758", Category = "Bên vận tải" },
        new() { Token = "{{OWNER_NAME}}", Label = "Họ tên Chủ phương tiện", SampleValue = "PHAN THỊ VIỆT HÀ", Category = "Chủ xe & Xe" },
        new() { Token = "{{OWNER_CITIZEN_ID}}", Label = "CCCD Chủ phương tiện", SampleValue = "092162002859", Category = "Chủ xe & Xe" },
        new() { Token = "{{OWNER_ISSUED_DATE}}", Label = "Ngày cấp CCCD Chủ xe", SampleValue = "29/04/2021", Category = "Chủ xe & Xe" },
        new() { Token = "{{VEHICLE_PLATE}}", Label = "Biển kiểm soát xe", SampleValue = "65E-003.89", Category = "Chủ xe & Xe" },
        new() { Token = "{{VEHICLE_BRAND_MODEL}}", Label = "Nhãn hiệu / Loại xe", SampleValue = "FORD TRANSIT", Category = "Chủ xe & Xe" },
        new() { Token = "{{SEAT_COUNT}}", Label = "Sức chứa (Số chỗ)", SampleValue = "16", Category = "Chủ xe & Xe" },
        new() { Token = "{{PASSENGER_COUNT_2D}}", Label = "Số lượng khách (02 chữ số)", SampleValue = "05", Category = "Chủ xe & Xe" },
        new() { Token = "{{DRIVER_NAME}}", Label = "Họ và tên lái xe 1", SampleValue = "NGUYỄN VĂN TÀI", Category = "Tài xế" },
        new() { Token = "{{DRIVER_LICENSE_CLASS}}", Label = "Hạng GPLX lái xe 1", SampleValue = "D", Category = "Tài xế" },
        new() { Token = "{{TRANSPORT_DRIVER1_LICENSE_NUMBER}}", Label = "Số GPLX lái xe 1", SampleValue = "920188001234", Category = "Tài xế" },
        new() { Token = "{{SECOND_DRIVER_NAME}}", Label = "Họ và tên lái xe 2", SampleValue = "Không có", Category = "Tài xế" },
        new() { Token = "{{SECOND_DRIVER_LICENSE_CLASS}}", Label = "Hạng GPLX lái xe 2", SampleValue = "N/A", Category = "Tài xế" },
        new() { Token = "{{TRANSPORT_DRIVER2_LICENSE_NUMBER}}", Label = "Số GPLX lái xe 2", SampleValue = "N/A", Category = "Tài xế" },
        new() { Token = "{{CUSTOMER_NAME}}", Label = "Tên cơ quan / người thuê xe", SampleValue = "Khách hàng cá nhân", Category = "Khách hàng" },
        new() { Token = "{{CUSTOMER_TAX_CODE}}", Label = "Mã số thuế khách hàng", SampleValue = "N/A", Category = "Khách hàng" },
        new() { Token = "{{CUSTOMER_ADDRESS}}", Label = "Địa chỉ khách hàng", SampleValue = "Phường Ninh Kiều, TP. Cần Thơ", Category = "Khách hàng" },
        new() { Token = "{{CUSTOMER_PHONE}}", Label = "Số điện thoại khách hàng", SampleValue = "0909888777", Category = "Khách hàng" },
        new() { Token = "{{CUSTOMER_REPRESENTATIVE}}", Label = "Người đại diện Bên thuê", SampleValue = "LÊ MINH KHANG", Category = "Khách hàng" },
        new() { Token = "{{CUSTOMER_CITIZEN_ID}}", Label = "CCCD khách hàng / đại diện", SampleValue = "092090005678", Category = "Khách hàng" },
        new() { Token = "{{CUSTOMER_ISSUED_DATE}}", Label = "Ngày cấp CCCD khách hàng", SampleValue = "15/06/2022", Category = "Khách hàng" },
        new() { Token = "{{PICKUP_DATETIME_LOCATION}}", Label = "Thời gian & địa điểm đón khách", SampleValue = "28/11/2026 07:00 - Bến Ninh Kiều, TP. Cần Thơ", Category = "Hành trình & Giá" },
        new() { Token = "{{DROPOFF_DATETIME_LOCATION}}", Label = "Thời gian & địa điểm trả khách", SampleValue = "28/11/2026 18:00 - Sân bay Tân Sơn Nhất, TP.HCM", Category = "Hành trình & Giá" },
        new() { Token = "{{PICKUP_LOCATION}}", Label = "Nơi nhận hàng / Điểm đi", SampleValue = "KCN Trà Nóc, TP. Cần Thơ", Category = "Hành trình & Giá" },
        new() { Token = "{{DROPOFF_LOCATION}}", Label = "Nơi trả hàng / Điểm đến", SampleValue = "KCN Tân Bình, TP. Hồ Chí Minh", Category = "Hành trình & Giá" },
        new() { Token = "{{CARGO_START_TIME}}", Label = "Thời gian vận chuyển từ", SampleValue = "28/11/2026 07:00", Category = "Hành trình & Giá" },
        new() { Token = "{{CARGO_END_TIME}}", Label = "Thời gian vận chuyển đến", SampleValue = "28/11/2026 18:00", Category = "Hành trình & Giá" },
        new() { Token = "{{ROUTE_DESCRIPTION}}", Label = "Hành trình xe chạy", SampleValue = "Cần Thơ - Quốc lộ 1A - Cao tốc Trung Lương - TP.HCM", Category = "Hành trình & Giá" },
        new() { Token = "{{TOTAL_KILOMETERS}}", Label = "Tổng km", SampleValue = "175,0", Category = "Hành trình & Giá" },
        new() { Token = "{{CONTRACT_VALUE_WITH_UNIT}}", Label = "Giá cước vận tải (số)", SampleValue = "3.500.000 đồng", Category = "Hành trình & Giá" },
        new() { Token = "{{CONTRACT_VALUE_WORDS}}", Label = "Giá cước bằng chữ", SampleValue = "Ba triệu năm trăm nghìn đồng", Category = "Hành trình & Giá" },
        new() { Token = "{{PAYMENT_METHOD}}", Label = "Hình thức thanh toán", SampleValue = "Chuyển khoản", Category = "Hành trình & Giá" },
        new() { Token = "{{PAYMENT_TIME}}", Label = "Thời gian thanh toán", SampleValue = "Thanh toán sau khi kết thúc chuyến đi", Category = "Hành trình & Giá" },
        new() { Token = "{{PAYMENT_DESCRIPTION}}", Label = "Mô tả thanh toán (HĐ hàng hóa)", SampleValue = "Chuyển khoản - Thanh toán sau khi giao đủ hàng", Category = "Hành trình & Giá" },
        new() { Token = "{{CARGO_NAME}}", Label = "Loại hàng hóa", SampleValue = "Nông sản đóng thùng", Category = "Hàng hóa" },
        new() { Token = "{{CARGO_SPECIFICATION}}", Label = "Quy cách đóng gói", SampleValue = "Thùng carton tiêu chuẩn", Category = "Hàng hóa" },
        new() { Token = "{{CARGO_QUANTITY}}", Label = "Số lượng hàng", SampleValue = "120 thùng", Category = "Hàng hóa" },
        new() { Token = "{{CARGO_WEIGHT_UNIT}}", Label = "Khối lượng hàng", SampleValue = "2.400,00 kg", Category = "Hàng hóa" },
        new() { Token = "{{SIG_OFFICE_NAME}}", Label = "Tên người ký Đại diện HTX", SampleValue = "NGUYỄN TRỌNG HẬU", Category = "Chữ ký" },
        new() { Token = "{{SIG_OWNER_NAME}}", Label = "Tên người ký Chủ xe", SampleValue = "PHAN THỊ VIỆT HÀ", Category = "Chữ ký" },
        new() { Token = "{{SIG_CUSTOMER_NAME}}", Label = "Tên người ký Khách hàng", SampleValue = "LÊ MINH KHANG", Category = "Chữ ký" },
        new() { Token = "{{SIG_DRIVER_NAME}}", Label = "Tên người ký Tài xế", SampleValue = "NGUYỄN VĂN TÀI", Category = "Chữ ký" }
    ];

    public string GetTemplatesJsonPath()
    {
        var configured = configuration["DocumentGeneration:MainContractTemplatesPath"];
        var relative = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine("Templates", "Contracts", "MainContractTemplates.json")
            : configured.Trim();

        return Path.IsPathRooted(relative)
            ? relative
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, relative));
    }

    public MainContractTemplateDto GetTemplate(
        ContractBusinessType businessType,
        Guid? companyProfileId = null,
        string? companyName = null)
    {
        var bundle = LoadBundle();
        var map = businessType == ContractBusinessType.Cargo
            ? bundle.CompanyCargoTemplates
            : bundle.CompanyPassengerTemplates;

        if (companyProfileId.HasValue &&
            map.TryGetValue(companyProfileId.Value.ToString("N"), out var custom))
        {
            return Clone(custom);
        }

        var preset = businessType == ContractBusinessType.Cargo
            ? bundle.DefaultCargo
            : bundle.DefaultPassenger;
        var clone = Clone(preset);
        clone.CompanyProfileId = companyProfileId;
        if (!string.IsNullOrWhiteSpace(companyName))
        {
            var typeLabel = businessType == ContractBusinessType.Cargo ? "HĐ Hàng hóa" : "HĐ Hành khách";
            clone.TemplateName = $"Mẫu {typeLabel} — {companyName}";
        }

        return clone;
    }

    public bool HasCustomCompanyTemplate(ContractBusinessType businessType, Guid companyProfileId)
    {
        var bundle = LoadBundle();
        var map = businessType == ContractBusinessType.Cargo
            ? bundle.CompanyCargoTemplates
            : bundle.CompanyPassengerTemplates;
        return map.ContainsKey(companyProfileId.ToString("N"));
    }

    public async Task SaveTemplateAsync(
        AuthenticationStateProvider authStateProvider,
        ContractBusinessType businessType,
        Guid? companyProfileId,
        MainContractTemplateDto template,
        CancellationToken ct = default)
    {
        await EnsureOwnerAccessAsync(authStateProvider);

        var path = GetTemplatesJsonPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await WriteLock.WaitAsync(ct);
        try
        {
            var bundle = LoadBundle();
            template.BusinessType = businessType;
            template.CompanyProfileId = companyProfileId;
            template.UpdatedAtUtc = DateTime.UtcNow;

            if (companyProfileId.HasValue)
            {
                var map = businessType == ContractBusinessType.Cargo
                    ? bundle.CompanyCargoTemplates
                    : bundle.CompanyPassengerTemplates;
                map[companyProfileId.Value.ToString("N")] = Clone(template);
            }
            else if (businessType == ContractBusinessType.Cargo)
            {
                bundle.DefaultCargo = Clone(template);
            }
            else
            {
                bundle.DefaultPassenger = Clone(template);
            }

            await PersistBundleAsync(path, bundle, ct);
            logger.LogInformation(
                "Owner đã lưu mẫu Hợp đồng Chính ({BusinessType}). CompanyProfileId={CompanyProfileId}, Path={Path}",
                businessType,
                companyProfileId,
                path);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public async Task<MainContractTemplateDto> ResetToDefaultAsync(
        AuthenticationStateProvider authStateProvider,
        ContractBusinessType businessType,
        Guid? companyProfileId,
        string? companyName = null,
        CancellationToken ct = default)
    {
        await EnsureOwnerAccessAsync(authStateProvider);

        var path = GetTemplatesJsonPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await WriteLock.WaitAsync(ct);
        try
        {
            var bundle = LoadBundle();
            var defaultPreset = CreateDefaultPreset(businessType);

            if (companyProfileId.HasValue)
            {
                var map = businessType == ContractBusinessType.Cargo
                    ? bundle.CompanyCargoTemplates
                    : bundle.CompanyPassengerTemplates;
                map.Remove(companyProfileId.Value.ToString("N"));
            }
            else if (businessType == ContractBusinessType.Cargo)
            {
                bundle.DefaultCargo = Clone(defaultPreset);
            }
            else
            {
                bundle.DefaultPassenger = Clone(defaultPreset);
            }

            await PersistBundleAsync(path, bundle, ct);
            defaultPreset.CompanyProfileId = companyProfileId;
            if (!string.IsNullOrWhiteSpace(companyName))
            {
                var typeLabel = businessType == ContractBusinessType.Cargo ? "HĐ Hàng hóa" : "HĐ Hành khách";
                defaultPreset.TemplateName = $"Mẫu {typeLabel} — {companyName}";
            }

            return defaultPreset;
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public async Task<MainContractTemplateDto> ImportFromDocxAsync(
        AuthenticationStateProvider authStateProvider,
        ContractBusinessType businessType,
        Guid? companyProfileId,
        Stream docxStream,
        string originalFileName,
        string? companyName = null,
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

        var mainContractsDir = Path.Combine(Path.GetDirectoryName(GetTemplatesJsonPath())!, "MainContracts");
        Directory.CreateDirectory(mainContractsDir);
        var typeCode = businessType == ContractBusinessType.Cargo ? "HangHoa" : "HanhKhach";
        var keySegment = companyProfileId?.ToString("N") ?? $"default-{typeCode}";
        var storedDocxName = $"MainContract_{typeCode}_{keySegment}_{DateTime.UtcNow:yyyyMMddHHmmss}.docx";
        var storedDocxPath = Path.Combine(mainContractsDir, storedDocxName);
        await File.WriteAllBytesAsync(storedDocxPath, memory.ToArray(), ct);

        var current = GetTemplate(businessType, companyProfileId, companyName);
        ParseParagraphsIntoTemplate(paragraphs, current);
        current.UploadedDocxFileName = originalFileName;
        current.UpdatedAtUtc = DateTime.UtcNow;

        await SaveTemplateAsync(authStateProvider, businessType, companyProfileId, current, ct);
        return current;
    }

    /// <summary>
    /// Xuất Mẫu Hợp đồng Chính (2 trang A4 chuẩn Times New Roman) ra file Word (.docx).
    /// - Trang 1: Nội dung hợp đồng + Trách nhiệm các bên + 3 cột chữ ký.
    /// - Trang 2: Bảng Danh sách hành khách 20 dòng (HĐ Hành khách) hoặc Bảng Giấy vận tải 5 mục + Xếp/Dỡ (HĐ Hàng hóa).
    /// </summary>
    public byte[] ExportTemplateAsDocxBytes(
        MainContractTemplateDto template,
        IReadOnlyDictionary<string, string>? replacements = null)
    {
        string R(string? text)
            => string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : replacements is null
                    ? text
                    : ReplacePlaceholders(text, replacements);

        using var stream = new MemoryStream();
        using (var wordDoc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = new Body();

            // ==================== TRANG 1/2 ====================
            AppendParagraph(body, "TRANG 1/2", bold: true, italic: false, center: false, rightAlign: true, fontSizeHalfPt: "18", spaceAfter: "20");

            if (template.BusinessType == ContractBusinessType.Cargo)
            {
                var headerTable = CreateBorderlessTwoColumnTable(
                    [
                        (R(template.LeftHeaderLine1), true, false, "21"),
                        (R(template.LeftHeaderLine2), true, false, "21")
                    ],
                    [
                        ("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", true, false, "22"),
                        ("Độc lập - Tự do - Hạnh phúc", true, false, "22")
                    ]);
                body.Append(headerTable);
            }
            else
            {
                AppendParagraph(body, "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", bold: true, italic: false, center: true, fontSizeHalfPt: "24", spaceAfter: "20");
                AppendParagraph(body, "Độc lập – Tự do – Hạnh phúc", bold: true, italic: false, center: true, fontSizeHalfPt: "23", spaceAfter: "60");
            }

            AppendParagraph(body, R(template.DocumentTitle), bold: true, italic: false, center: true, fontSizeHalfPt: "28", spaceAfter: "20");
            AppendParagraph(body, R(template.ContractNumberLine), bold: false, italic: false, center: true, fontSizeHalfPt: "22", spaceAfter: "80");

            if (!string.IsNullOrWhiteSpace(template.LegalBasesText))
                AppendMultiLineBlock(body, R(template.LegalBasesText), bold: false, italic: false, fontSizeHalfPt: "21");

            AppendMultiLineBlock(body, R(template.IntroDateLocationText), bold: false, italic: false, fontSizeHalfPt: "21");

            // Bên A
            AppendParagraph(body, R(template.PartyASectionTitle), bold: true, italic: false, center: false, fontSizeHalfPt: "22", spaceAfter: "40");
            AppendMultiLineBlock(body, R(template.PartyABodyText), bold: false, italic: false, fontSizeHalfPt: "21");

            // Bên B
            AppendParagraph(body, R(template.PartyBSectionTitle), bold: true, italic: false, center: false, fontSizeHalfPt: "22", spaceAfter: "40");
            AppendMultiLineBlock(body, R(template.PartyBBodyText), bold: false, italic: false, fontSizeHalfPt: "21");

            // Nội dung vận chuyển (Hành khách hoặc Điều 1-2-3 Hàng hóa)
            if (!string.IsNullOrWhiteSpace(template.TransportDetailsTitle))
                AppendParagraph(body, R(template.TransportDetailsTitle), bold: false, italic: false, center: false, fontSizeHalfPt: "21", spaceAfter: "40");
            AppendMultiLineBlock(body, R(template.TransportDetailsBodyText), bold: false, italic: false, fontSizeHalfPt: "21");

            // III. Trách nhiệm các bên
            AppendParagraph(body, R(template.ResponsibilitiesSectionTitle), bold: true, italic: false, center: false, fontSizeHalfPt: "22", spaceAfter: "40");

            AppendParagraph(body, R(template.ResponsibilityHtxTitle), bold: true, italic: false, center: false, fontSizeHalfPt: "21", spaceAfter: "20");
            AppendMultiLineBlock(body, R(template.ResponsibilityHtxBodyText), bold: false, italic: false, fontSizeHalfPt: "20");

            AppendParagraph(body, R(template.ResponsibilityOwnerTitle), bold: true, italic: false, center: false, fontSizeHalfPt: "21", spaceAfter: "20");
            AppendMultiLineBlock(body, R(template.ResponsibilityOwnerBodyText), bold: false, italic: false, fontSizeHalfPt: "20");

            AppendParagraph(body, R(template.ResponsibilityCustomerTitle), bold: true, italic: false, center: false, fontSizeHalfPt: "21", spaceAfter: "20");
            AppendMultiLineBlock(body, R(template.ResponsibilityCustomerBodyText), bold: false, italic: false, fontSizeHalfPt: "20");

            AppendMultiLineBlock(body, R(template.GeneralCommitmentText), bold: false, italic: false, fontSizeHalfPt: "20");

            // Bảng Chữ ký 3 cột Trang 1
            var sigOffice = R("{{SIG_OFFICE_NAME}}").ToUpper(CultureInfo.GetCultureInfo("vi-VN"));
            var sigOwner = R("{{SIG_OWNER_NAME}}").ToUpper(CultureInfo.GetCultureInfo("vi-VN"));
            var sigCustomer = R("{{SIG_CUSTOMER_NAME}}").ToUpper(CultureInfo.GetCultureInfo("vi-VN"));

            var sigTablePage1 = CreateBorderlessThreeColumnTable(
                [
                    (R(template.Page1SigLeftTitle), true, false, "22"),
                    (string.Empty, false, false, "20"),
                    (string.Empty, false, false, "20"),
                    (string.Empty, false, false, "20"),
                    (sigOffice, true, false, "21")
                ],
                [
                    (R(template.Page1SigCenterTitle), true, false, "22"),
                    (string.Empty, false, false, "20"),
                    (string.Empty, false, false, "20"),
                    (string.Empty, false, false, "20"),
                    (sigOwner, true, false, "21")
                ],
                [
                    (R(template.Page1SigRightTitle), true, false, "22"),
                    (string.Empty, false, false, "20"),
                    (string.Empty, false, false, "20"),
                    (string.Empty, false, false, "20"),
                    (sigCustomer, true, false, "21")
                ]);
            body.Append(sigTablePage1);

            // ==================== NGẮT SANG TRANG 2/2 ====================
            body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));

            AppendParagraph(body, "TRANG 2/2", bold: true, italic: false, center: false, rightAlign: true, fontSizeHalfPt: "18", spaceAfter: "20");

            var page2HeaderTable = CreateBorderlessTwoColumnTable(
                [
                    (R(template.Page2LeftHeaderLine1), true, false, "20"),
                    (R(template.Page2LeftHeaderLine2), true, false, "20")
                ],
                [
                    ("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", true, false, "22"),
                    ("Độc lập - Tự do - Hạnh phúc", true, false, "21")
                ]);
            body.Append(page2HeaderTable);

            AppendParagraph(body, R(template.Page2Title), bold: true, italic: false, center: true, fontSizeHalfPt: "28", spaceAfter: "30");
            AppendMultiLineBlock(body, R(template.Page2Subtitle), bold: false, italic: true, fontSizeHalfPt: "21", center: true);

            if (template.BusinessType == ContractBusinessType.Passenger)
            {
                body.Append(BuildPassengerListWordTable(R, template));
            }
            else
            {
                body.Append(BuildCargoTransportPaperWordTable(R));
            }

            AppendParagraph(body, string.Empty, bold: false, italic: false, fontSizeHalfPt: "18", spaceAfter: "40");

            var sigDriver = R("{{SIG_DRIVER_NAME}}").ToUpper(CultureInfo.GetCultureInfo("vi-VN"));
            var sigTablePage2 = CreateBorderlessTwoColumnTable(
                [
                    (R(template.Page2SigLeftTitle), true, false, "22"),
                    (R(template.Page2SigLeftSubTitle), false, true, "19"),
                    (string.Empty, false, false, "20"),
                    (string.Empty, false, false, "20"),
                    (string.Empty, false, false, "20"),
                    (sigCustomer, true, false, "21")
                ],
                [
                    (R(template.Page2SigRightTitle), true, false, "22"),
                    (R(template.Page2SigRightSubTitle), false, true, "19"),
                    (string.Empty, false, false, "20"),
                    (string.Empty, false, false, "20"),
                    (string.Empty, false, false, "20"),
                    (sigDriver, true, false, "21")
                ]);
            body.Append(sigTablePage2);

            // Cấu hình trang giấy A4 chuẩn
            body.Append(new SectionProperties(
                new PageSize { Width = 11906U, Height = 16838U },
                new PageMargin { Top = 720, Right = 720, Bottom = 720, Left = 900 }));

            mainPart.Document.Append(body);
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    public static Dictionary<string, string> BuildContractPlaceholders(Contract contract)
    {
        var snapshot = ContractSnapshotData.FromJson(contract.ContractDataJson);
        var company = contract.CompanyProfile;
        var customer = contract.Customer;
        var vehicle = contract.Vehicle;
        var driver = contract.Driver;
        var contractDate = contract.CreatedAt.Kind == DateTimeKind.Utc
            ? contract.CreatedAt.AddHours(7)
            : contract.CreatedAt;

        var passengerCount = contract.ActualPassengerCount ??
            (contract.Passengers.Count(x => !x.IsDeleted && !string.IsNullOrWhiteSpace(x.FullName)) +
             (contract.CustomerTravelsWithGroup ? 1 : 0));

        var companyName = snapshot is not null
            ? First(snapshot.Company.Name, "...")
            : First(contract.CompanyNameSnapshot, company?.CompanyName, "...");

        var companyComplaintContact = snapshot is not null
            ? First(snapshot.Company.ComplaintContact, CompanyProfile.DefaultComplaintContact)
            : First(company?.ComplaintContact, CompanyProfile.DefaultComplaintContact);

        var liveCustomerIsCompany = customer?.Type == CustomerType.Organization ||
            !string.IsNullOrWhiteSpace(customer?.OrganizationName);
        var snapshotCustomerIsCompany = snapshot is not null &&
            !string.IsNullOrWhiteSpace(snapshot.Customer.OrganizationName);
        var customerIsCompany = snapshotCustomerIsCompany || liveCustomerIsCompany;

        var customerLegalName = snapshot is not null
            ? First(
                snapshot.Customer.OrganizationName,
                customerIsCompany ? customer?.OrganizationName : null,
                customerIsCompany ? contract.CustomerNameSnapshot : null,
                snapshot.Customer.FullName,
                "...")
            : First(contract.CustomerNameSnapshot, customer?.OrganizationName, customer?.FullName, "...");
        var customerName = customerIsCompany ? customerLegalName : "Khách hàng cá nhân";

        var customerRepresentative = customerIsCompany
            ? First(snapshot?.Customer.FullName, customer?.FullName, contract.CustomerNameSnapshot, "...")
            : snapshot is not null
                ? First(snapshot.Customer.FullName, "...")
                : First(customer?.FullName, contract.CustomerNameSnapshot, "...");

        var ownerName = snapshot is not null
            ? First(snapshot.Vehicle.OwnerName, "...")
            : First(contract.VehicleOwnerNameSnapshot, vehicle?.OwnerName, "...");
        var driverName = snapshot is not null
            ? First(snapshot.Driver.FullName, "...")
            : First(contract.OperatingDriverName, contract.DriverNameSnapshot, driver?.FullName, "...");
        var vehicleBrandModel = snapshot is not null
            ? First(snapshot.Vehicle.BrandModel, "...")
            : First(
                contract.VehicleBrandSnapshot,
                string.Join(" ", new[] { vehicle?.Brand, vehicle?.Model, vehicle?.VehicleType }
                    .Where(x => !string.IsNullOrWhiteSpace(x))),
                "...");

        var companyTaxCode = snapshot is not null
            ? First(snapshot.Company.TaxCode, contract.CompanyTaxCodeSnapshot, company?.TaxCode, "...")
            : First(contract.CompanyTaxCodeSnapshot, company?.TaxCode, "...");
        var companyLicense = snapshot is not null
            ? First(snapshot.Company.BusinessLicenseNumber, company?.BusinessLicenseNumber, "...")
            : First(company?.BusinessLicenseNumber, "...");
        var companyLicensePlace = snapshot is not null
            ? First(snapshot.Company.BusinessLicenseIssuedPlace, company?.BusinessLicenseIssuedPlace, CompanyProfile.DefaultBusinessLicenseIssuedPlace)
            : First(company?.BusinessLicenseIssuedPlace, CompanyProfile.DefaultBusinessLicenseIssuedPlace);
        var companyAddress = snapshot is not null
            ? First(snapshot.Company.Address, contract.CompanyAddressSnapshot, company?.Address, "...")
            : First(contract.CompanyAddressSnapshot, company?.Address, "...");
        var companyPhone = snapshot is not null
            ? First(snapshot.Company.PhoneNumber, company?.PhoneNumber, "...")
            : First(company?.PhoneNumber, "...");
        var companyRep = snapshot is not null
            ? First(snapshot.Company.RepresentativeName, contract.CompanyRepresentativeSnapshot, company?.RepresentativeName, "...")
            : First(contract.CompanyRepresentativeSnapshot, company?.RepresentativeName, "...");
        var companyRepCitizenId = snapshot is not null
            ? First(snapshot.Company.RepresentativeCitizenId, company?.RepresentativeCitizenId, "...")
            : First(company?.RepresentativeCitizenId, "...");
        var companyRepIssuedDate = FormatDateOnly(snapshot is not null
            ? snapshot.Company.RepresentativeCitizenIdIssuedDate ?? company?.RepresentativeCitizenIdIssuedDate
            : company?.RepresentativeCitizenIdIssuedDate);

        var ownerCitizenId = snapshot is not null
            ? First(snapshot.Vehicle.OwnerCitizenId, contract.VehicleOwnerCitizenIdSnapshot, vehicle?.OwnerCitizenId, "...")
            : First(contract.VehicleOwnerCitizenIdSnapshot, vehicle?.OwnerCitizenId, "...");
        var ownerIssuedDate = FormatDateOnly(snapshot is not null
            ? snapshot.Vehicle.OwnerCitizenIdIssuedDate ?? vehicle?.OwnerCitizenIdIssuedDate
            : vehicle?.OwnerCitizenIdIssuedDate);

        var customerTaxCode = customerIsCompany
            ? First(snapshot?.Customer.TaxCode, customer?.TaxCode, "N/A")
            : "N/A";
        var customerPhone = snapshot is not null
            ? First(snapshot.Customer.PhoneNumber, contract.CustomerPhoneSnapshot, customer?.PhoneNumber, "...")
            : First(contract.CustomerPhoneSnapshot, customer?.PhoneNumber, "...");
        var customerAddress = snapshot is not null
            ? First(snapshot.Customer.Address, contract.CustomerAddressSnapshot, customer?.Address, "...")
            : First(contract.CustomerAddressSnapshot, customer?.Address, "...");
        var customerCitizenId = snapshot is not null
            ? First(snapshot.Customer.CitizenId, contract.CustomerCitizenIdSnapshot, customer?.CitizenId, "...")
            : First(contract.CustomerCitizenIdSnapshot, customer?.CitizenId, "...");
        var customerIssuedDate = FormatDateOnly(snapshot is not null
            ? snapshot.Customer.CitizenIdIssuedDate ?? customer?.CitizenIdIssuedDate
            : customer?.CitizenIdIssuedDate);

        var plateNumber = snapshot is not null
            ? First(snapshot.Vehicle.PlateNumber, contract.VehiclePlateSnapshot, vehicle?.PlateNumber, "...")
            : First(contract.VehiclePlateSnapshot, vehicle?.PlateNumber, "...");
        var seatCount = (snapshot is not null ? snapshot.Vehicle.SeatCount : vehicle?.SeatCount)?.ToString(CultureInfo.InvariantCulture) ?? "...";
        var driverLicenseClass = snapshot is not null
            ? First(snapshot.Driver.DriverLicenseClass, contract.OperatingDriverLicenseClass, contract.DriverLicenseClassSnapshot, driver?.DriverLicenseClass, "...")
            : First(contract.OperatingDriverLicenseClass, contract.DriverLicenseClassSnapshot, driver?.DriverLicenseClass, "...");
        var driver1LicenseNumber = First(contract.OperatingDriverLicenseNumber, snapshot?.Driver.DriverLicenseNumber, contract.DriverLicenseNumberSnapshot, "N/A");

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{{CONTRACT_NUMBER}}"] = First(contract.ContractNumber, "..."),
            ["{{CONTRACT_TIME}}"] = contractDate.ToString("HH:mm", CultureInfo.InvariantCulture),
            ["{{CONTRACT_DAY}}"] = contractDate.ToString("dd", CultureInfo.InvariantCulture),
            ["{{CONTRACT_MONTH}}"] = contractDate.ToString("MM", CultureInfo.InvariantCulture),
            ["{{CONTRACT_YEAR}}"] = contractDate.ToString("yyyy", CultureInfo.InvariantCulture),
            ["{{HEADER_COMPANY_LICENSE_ISSUED_PLACE}}"] = companyLicensePlace,
            ["{{HEADER_COMPANY_OFFICE_NAME}}"] = companyName,
            ["{{COMPANY_NAME}}"] = companyName,
            ["{{COMPANY_OFFICE_NAME}}"] = companyName,
            ["{{COMPANY_TAX_CODE}}"] = companyTaxCode,
            ["{{COMPANY_LICENSE}}"] = companyLicense,
            ["{{COMPANY_LICENSE_ISSUED_PLACE}}"] = companyLicensePlace,
            ["{{COMPANY_ADDRESS}}"] = companyAddress,
            ["{{COMPANY_PHONE}}"] = companyPhone,
            ["{{COMPANY_COMPLAINT_CONTACT}}"] = companyComplaintContact,
            ["{{COMPANY_REPRESENTATIVE}}"] = companyRep,
            ["{{COMPANY_REP_CITIZEN_ID}}"] = companyRepCitizenId,
            ["{{COMPANY_REP_ISSUED_DATE}}"] = companyRepIssuedDate,
            ["{{OWNER_NAME}}"] = ownerName,
            ["{{OWNER_CITIZEN_ID}}"] = ownerCitizenId,
            ["{{OWNER_ISSUED_DATE}}"] = ownerIssuedDate,
            ["{{CUSTOMER_NAME}}"] = customerName,
            ["{{CUSTOMER_TAX_CODE}}"] = customerTaxCode,
            ["{{CUSTOMER_PHONE}}"] = customerPhone,
            ["{{CUSTOMER_ADDRESS}}"] = customerAddress,
            ["{{CUSTOMER_CITIZEN_ID}}"] = customerCitizenId,
            ["{{CUSTOMER_ISSUED_DATE}}"] = customerIssuedDate,
            ["{{CUSTOMER_REPRESENTATIVE}}"] = customerRepresentative,
            ["{{VEHICLE_PLATE}}"] = plateNumber,
            ["{{VEHICLE_BRAND_MODEL}}"] = vehicleBrandModel,
            ["{{SEAT_COUNT}}"] = seatCount,
            ["{{PASSENGER_COUNT}}"] = passengerCount.ToString(CultureInfo.InvariantCulture),
            ["{{PASSENGER_COUNT_2D}}"] = passengerCount.ToString("00", CultureInfo.InvariantCulture),
            ["{{DRIVER_NAME}}"] = driverName,
            ["{{DRIVER_LICENSE_CLASS}}"] = driverLicenseClass,
            ["{{SECOND_DRIVER_NAME}}"] = First(contract.SecondDriverName, "Không có"),
            ["{{SECOND_DRIVER_LICENSE_CLASS}}"] = First(contract.SecondDriverLicenseClass, "N/A"),
            ["{{PICKUP_DATETIME_LOCATION}}"] = BuildDateTimeLocation(contract.StartTime, contract.PickupLocation),
            ["{{DROPOFF_DATETIME_LOCATION}}"] = BuildDateTimeLocation(contract.EndTime, contract.DropoffLocation),
            ["{{PICKUP_LOCATION}}"] = First(contract.PickupLocation, "..."),
            ["{{DROPOFF_LOCATION}}"] = First(contract.DropoffLocation, "..."),
            ["{{ROUTE_DESCRIPTION}}"] = First(contract.RouteDescription, "..."),
            ["{{TOTAL_KILOMETERS}}"] = contract.TotalKilometers?.ToString("N1", CultureInfo.GetCultureInfo("vi-VN")) ?? "...",
            ["{{CONTRACT_VALUE}}"] = contract.ContractValue?.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) ?? "...",
            ["{{CONTRACT_VALUE_WITH_UNIT}}"] = contract.ContractValue is null
                ? "..."
                : $"{contract.ContractValue.Value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} đồng",
            ["{{CONTRACT_VALUE_WORDS}}"] = Capitalize(NumberToVietnameseWords(contract.ContractValue)) + " đồng",
            ["{{PAYMENT_METHOD}}"] = First(contract.PaymentMethod, "..."),
            ["{{PAYMENT_TIME}}"] = First(contract.PaymentTime, "..."),
            ["{{PAYMENT_DESCRIPTION}}"] = JoinNonEmpty(" - ", contract.PaymentMethod, contract.PaymentTime),
            ["{{CARGO_NAME}}"] = First(contract.CargoName, "..."),
            ["{{CARGO_SPECIFICATION}}"] = First(contract.CargoSpecification, "..."),
            ["{{CARGO_QUANTITY}}"] = First(contract.CargoQuantity, "..."),
            ["{{CARGO_WEIGHT_UNIT}}"] = contract.CargoWeight is null
                ? "..."
                : string.IsNullOrWhiteSpace(contract.CargoUnit)
                    ? contract.CargoWeight.Value.ToString("N2", CultureInfo.GetCultureInfo("vi-VN"))
                    : $"{contract.CargoWeight.Value.ToString("N2", CultureInfo.GetCultureInfo("vi-VN"))} {contract.CargoUnit.Trim()}",
            ["{{CARGO_START_TIME}}"] = FormatDateTime(contract.StartTime),
            ["{{CARGO_END_TIME}}"] = FormatDateTime(contract.EndTime),
            ["{{TRANSPORT_CONTRACT_NUMBER}}"] = First(contract.ContractNumber, "N/A"),
            ["{{TRANSPORT_VALID_UNTIL}}"] = contract.EndTime is null ? "N/A" : VietnamTime(contract.EndTime.Value).ToString("dd/MM/yyyy"),
            ["{{TRANSPORT_VEHICLE_PLATE}}"] = plateNumber,
            ["{{TRANSPORT_COMPANY_NAME}}"] = companyName,
            ["{{TRANSPORT_COMPANY_TAX_CODE}}"] = companyTaxCode,
            ["{{TRANSPORT_COMPANY_LICENSE}}"] = companyLicense,
            ["{{TRANSPORT_COMPANY_ADDRESS}}"] = companyAddress,
            ["{{TRANSPORT_COMPANY_REPRESENTATIVE}}"] = companyRep,
            ["{{TRANSPORT_COMPANY_REP_CITIZEN_ID}}"] = companyRepCitizenId,
            ["{{TRANSPORT_COMPANY_PHONE}}"] = companyPhone,
            ["{{TRANSPORT_DRIVER1_NAME}}"] = First(driverName, "N/A"),
            ["{{TRANSPORT_DRIVER1_LICENSE_NUMBER}}"] = driver1LicenseNumber,
            ["{{TRANSPORT_DRIVER2_NAME}}"] = First(contract.SecondDriverName, "Không có"),
            ["{{TRANSPORT_DRIVER2_LICENSE_NUMBER}}"] = string.IsNullOrWhiteSpace(contract.SecondDriverName)
                ? "N/A"
                : First(contract.SecondDriverLicenseNumber, "N/A"),
            ["{{TRANSPORT_ROUTE}}"] = First(contract.CargoTransportRoute, "N/A"),
            ["{{TRANSPORT_GOODS_NAME}}"] = First(contract.CargoTransportGoodsName, "N/A"),
            ["{{TRANSPORT_LOADING_POINT}}"] = First(contract.CargoLoadingPoint, "N/A"),
            ["{{TRANSPORT_CARGO_WEIGHT}}"] = contract.CargoWeight is null
                ? "N/A"
                : contract.CargoWeight.Value.ToString("N2", CultureInfo.GetCultureInfo("vi-VN")),
            ["{{TRANSPORT_DELIVERY_POINT}}"] = First(contract.CargoDeliveryPoint, "N/A"),
            ["{{TRANSPORT_OTHER_INFO}}"] = First(contract.CargoOtherInformation, "N/A"),
            ["{{SIG_OFFICE_NAME}}"] = companyRep,
            ["{{SIG_OWNER_NAME}}"] = ownerName,
            ["{{SIG_CUSTOMER_NAME}}"] = customerRepresentative,
            ["{{SIG_DRIVER_NAME}}"] = driverName
        };

        foreach (var type in new[] { CargoHandlingType.Loading, CargoHandlingType.Unloading })
        {
            var prefix = type == CargoHandlingType.Loading ? "LOAD" : "UNLOAD";
            var rows = contract.CargoHandlingEvents
                .Where(x => !x.IsDeleted && x.Type == type)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.CreatedAt)
                .Take(3)
                .ToList();

            for (var index = 1; index <= 3; index++)
            {
                var item = index <= rows.Count ? rows[index - 1] : null;
                values[$"{{{{{prefix}{index}_LOCATION}}}}"] = First(item?.Location, "N/A");
                values[$"{{{{{prefix}{index}_WEIGHT}}}}"] = First(item?.CargoWeight, "N/A");
                values[$"{{{{{prefix}{index}_TIME}}}}"] = item?.EventTime is null
                    ? "N/A"
                    : VietnamTime(item.EventTime.Value).ToString("dd/MM/yyyy HH:mm");
                values[$"{{{{{prefix}{index}_CONFIRMATION}}}}"] = First(item?.Confirmation, "N/A");
            }
        }

        var passengerRows = new List<(string Name, int? BirthYear, string? Note)>();
        if (contract.CustomerTravelsWithGroup &&
            !string.IsNullOrWhiteSpace(customerRepresentative) &&
            !string.Equals(customerRepresentative, "...", StringComparison.Ordinal))
        {
            passengerRows.Add((
                customerRepresentative.Trim(),
                contract.CustomerTravelBirthYear ??
                    (snapshot is not null ? snapshot.Customer.DateOfBirth?.Year : customer?.DateOfBirth?.Year),
                contract.CustomerTravelNote));
        }

        passengerRows.AddRange(contract.Passengers
            .Where(x => !x.IsDeleted && !string.IsNullOrWhiteSpace(x.FullName))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .Select(x => (x.FullName.Trim(), x.BirthYear, x.Note)));

        passengerRows = passengerRows.Take(20).ToList();

        for (var index = 1; index <= 20; index++)
        {
            var passenger = index <= passengerRows.Count ? passengerRows[index - 1] : default;
            values[$"{{{{P{index:00}_NAME}}}}"] = passenger.Name ?? string.Empty;
            values[$"{{{{P{index:00}_BIRTH_YEAR}}}}"] = passenger.BirthYear?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            values[$"{{{{P{index:00}_NOTE}}}}"] = passenger.Note?.Trim() ?? string.Empty;
        }

        return values;
    }

    public static Dictionary<string, string> BuildSamplePlaceholders(CompanyProfile? company = null)
    {
        var map = SupportedPlaceholders.ToDictionary(
            x => x.Token,
            x => x.SampleValue,
            StringComparer.OrdinalIgnoreCase);

        if (company is not null)
        {
            map["{{COMPANY_NAME}}"] = First(company.CompanyName, map["{{COMPANY_NAME}}"]);
            map["{{COMPANY_OFFICE_NAME}}"] = First(company.CompanyName, map["{{COMPANY_OFFICE_NAME}}"]);
            map["{{HEADER_COMPANY_OFFICE_NAME}}"] = First(company.CompanyName, map["{{COMPANY_OFFICE_NAME}}"]);
            map["{{COMPANY_TAX_CODE}}"] = First(company.TaxCode, map["{{COMPANY_TAX_CODE}}"]);
            map["{{COMPANY_LICENSE}}"] = First(company.BusinessLicenseNumber, map["{{COMPANY_LICENSE}}"]);
            map["{{COMPANY_LICENSE_ISSUED_PLACE}}"] = First(company.BusinessLicenseIssuedPlace, CompanyProfile.DefaultBusinessLicenseIssuedPlace);
            map["{{HEADER_COMPANY_LICENSE_ISSUED_PLACE}}"] = First(company.BusinessLicenseIssuedPlace, CompanyProfile.DefaultBusinessLicenseIssuedPlace);
            map["{{COMPANY_ADDRESS}}"] = First(company.Address, map["{{COMPANY_ADDRESS}}"]);
            map["{{COMPANY_PHONE}}"] = First(company.PhoneNumber, map["{{COMPANY_PHONE}}"]);
            map["{{COMPANY_COMPLAINT_CONTACT}}"] = First(company.ComplaintContact, CompanyProfile.DefaultComplaintContact);
            map["{{COMPANY_REPRESENTATIVE}}"] = First(company.RepresentativeName, map["{{COMPANY_REPRESENTATIVE}}"]);
            map["{{COMPANY_REP_CITIZEN_ID}}"] = First(company.RepresentativeCitizenId, map["{{COMPANY_REP_CITIZEN_ID}}"]);
            map["{{COMPANY_REP_ISSUED_DATE}}"] = FormatDateOnly(company.RepresentativeCitizenIdIssuedDate);
            map["{{SIG_OFFICE_NAME}}"] = First(company.RepresentativeName, map["{{SIG_OFFICE_NAME}}"]);
        }
        else
        {
            map["{{HEADER_COMPANY_OFFICE_NAME}}"] = map["{{COMPANY_OFFICE_NAME}}"];
            map["{{HEADER_COMPANY_LICENSE_ISSUED_PLACE}}"] = map["{{COMPANY_LICENSE_ISSUED_PLACE}}"];
        }

        map["{{TRANSPORT_CONTRACT_NUMBER}}"] = map["{{CONTRACT_NUMBER}}"];
        map["{{TRANSPORT_VALID_UNTIL}}"] = "28/11/2026";
        map["{{TRANSPORT_VEHICLE_PLATE}}"] = map["{{VEHICLE_PLATE}}"];
        map["{{TRANSPORT_COMPANY_NAME}}"] = map["{{COMPANY_NAME}}"];
        map["{{TRANSPORT_COMPANY_TAX_CODE}}"] = map["{{COMPANY_TAX_CODE}}"];
        map["{{TRANSPORT_COMPANY_LICENSE}}"] = map["{{COMPANY_LICENSE}}"];
        map["{{TRANSPORT_COMPANY_ADDRESS}}"] = map["{{COMPANY_ADDRESS}}"];
        map["{{TRANSPORT_COMPANY_REPRESENTATIVE}}"] = map["{{COMPANY_REPRESENTATIVE}}"];
        map["{{TRANSPORT_COMPANY_REP_CITIZEN_ID}}"] = map["{{COMPANY_REP_CITIZEN_ID}}"];
        map["{{TRANSPORT_COMPANY_PHONE}}"] = map["{{COMPANY_PHONE}}"];
        map["{{TRANSPORT_DRIVER1_NAME}}"] = map["{{DRIVER_NAME}}"];
        map["{{TRANSPORT_DRIVER2_NAME}}"] = map["{{SECOND_DRIVER_NAME}}"];
        map["{{TRANSPORT_ROUTE}}"] = map["{{ROUTE_DESCRIPTION}}"];
        map["{{TRANSPORT_GOODS_NAME}}"] = map["{{CARGO_NAME}}"];
        map["{{TRANSPORT_LOADING_POINT}}"] = map["{{PICKUP_LOCATION}}"];
        map["{{TRANSPORT_CARGO_WEIGHT}}"] = "2.400,00";
        map["{{TRANSPORT_DELIVERY_POINT}}"] = map["{{DROPOFF_LOCATION}}"];
        map["{{TRANSPORT_OTHER_INFO}}"] = "Hàng nguyên đai nguyên kiện";

        for (var i = 1; i <= 3; i++)
        {
            map[$"{{{{LOAD{i}_LOCATION}}}}"] = i == 1 ? map["{{PICKUP_LOCATION}}"] : "N/A";
            map[$"{{{{LOAD{i}_WEIGHT}}}}"] = i == 1 ? "2.400 kg" : "N/A";
            map[$"{{{{LOAD{i}_TIME}}}}"] = i == 1 ? "28/11/2026 07:00" : "N/A";
            map[$"{{{{LOAD{i}_CONFIRMATION}}}}"] = i == 1 ? "Đã xếp đủ" : "N/A";

            map[$"{{{{UNLOAD{i}_LOCATION}}}}"] = i == 1 ? map["{{DROPOFF_LOCATION}}"] : "N/A";
            map[$"{{{{UNLOAD{i}_WEIGHT}}}}"] = i == 1 ? "2.400 kg" : "N/A";
            map[$"{{{{UNLOAD{i}_TIME}}}}"] = i == 1 ? "28/11/2026 17:30" : "N/A";
            map[$"{{{{UNLOAD{i}_CONFIRMATION}}}}"] = i == 1 ? "Đã giao đủ" : "N/A";
        }

        (string Name, string Year, string Note)[] samplePassengers =
        [
            ("LÊ MINH KHANG", "1990", "Trưởng đoàn"),
            ("TRẦN THỊ NGỌC ANH", "1993", ""),
            ("NGUYỄN HOÀNG PHÚC", "1988", ""),
            ("PHẠM THỊ KIM NGÂN", "1995", ""),
            ("VÕ QUỐC ĐẠT", "2001", "")
        ];

        for (var i = 1; i <= 20; i++)
        {
            var p = i <= samplePassengers.Length ? samplePassengers[i - 1] : (string.Empty, string.Empty, string.Empty);
            map[$"{{{{P{i:00}_NAME}}}}"] = p.Item1;
            map[$"{{{{P{i:00}_BIRTH_YEAR}}}}"] = p.Item2;
            map[$"{{{{P{i:00}_NOTE}}}}"] = p.Item3;
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

    /// <summary>
    /// Convert chuẩn xác 100% nội dung từ 2 file PDF gốc:
    /// - HopDongVanChuyenHanhKhach.template.pdf (HĐ Vận chuyển Hành khách 2 trang)
    /// - HopDongVanChuyenHangHoa.template.pdf (HĐ Vận chuyển Hàng hóa bằng xe ô tô 2 trang)
    /// </summary>
    public static MainContractTemplateDto CreateDefaultPreset(ContractBusinessType businessType)
    {
        if (businessType == ContractBusinessType.Cargo)
        {
            return new MainContractTemplateDto
            {
                BusinessType = ContractBusinessType.Cargo,
                TemplateName = "Mẫu Hợp đồng Vận chuyển Hàng hóa bằng xe ô tô (Convert từ PDF gốc 2 trang)",
                LeftHeaderLine1 = "{{HEADER_COMPANY_LICENSE_ISSUED_PLACE}}",
                LeftHeaderLine2 = "{{HEADER_COMPANY_OFFICE_NAME}}",
                DocumentTitle = "HỢP ĐỒNG VẬN CHUYỂN HÀNG HÓA BẰNG XE Ô TÔ",
                ContractNumberLine = "Số: {{CONTRACT_NUMBER}}/HĐVC",
                LegalBasesText = string.Empty,
                IntroDateLocationText = "Hôm nay, ngày {{CONTRACT_DAY}} tháng {{CONTRACT_MONTH}} năm {{CONTRACT_YEAR}} Hai bên chúng tôi gồm có:",
                PartyASectionTitle = "BÊN A: {{CUSTOMER_NAME}}   -   MST: {{CUSTOMER_TAX_CODE}}",
                PartyABodyText = """
                    Người đại diện: {{CUSTOMER_REPRESENTATIVE}}   -   CCCD: {{CUSTOMER_CITIZEN_ID}}   -   Ngày cấp: {{CUSTOMER_ISSUED_DATE}}
                    - Điện thoại: {{CUSTOMER_PHONE}}
                    - Địa chỉ: {{CUSTOMER_ADDRESS}}
                    """,
                PartyBSectionTitle = "BÊN B: {{COMPANY_NAME}}   (MST: {{COMPANY_TAX_CODE}}   -   Số GPKDVT: {{COMPANY_LICENSE}})",
                PartyBBodyText = """
                    - Địa chỉ trụ sở: {{COMPANY_ADDRESS}}
                    - Điện thoại: {{COMPANY_PHONE}}
                    - Người đại diện: {{COMPANY_REPRESENTATIVE}}   -   CCCD: {{COMPANY_REP_CITIZEN_ID}}   -   Ngày cấp: {{COMPANY_REP_ISSUED_DATE}}
                    - Chủ phương tiện: {{OWNER_NAME}}   -   CCCD: {{OWNER_CITIZEN_ID}}   -   Ngày cấp: {{OWNER_ISSUED_DATE}}
                    """,
                TransportDetailsTitle = "Hai bên cùng nhau thỏa thuận, ký kết hợp đồng vận chuyển hàng hóa với các điều khoản sau:",
                TransportDetailsBodyText = """
                    Điều 1. Bên B vận chuyển cho bên A những hàng hóa sau:
                    - Loại hàng: {{CARGO_NAME}}
                    - Quy cách: {{CARGO_SPECIFICATION}}   -   Số lượng: {{CARGO_QUANTITY}}   -   Khối lượng: {{CARGO_WEIGHT_UNIT}}
                    Điều 2. Địa điểm, phương thức giao nhận hàng hóa:
                    - Nơi nhận hàng: {{PICKUP_LOCATION}}
                    - Nơi trả hàng: {{DROPOFF_LOCATION}}
                    - Thời gian vận chuyển: Từ {{CARGO_START_TIME}} đến {{CARGO_END_TIME}}
                    - Hành trình xe chạy: {{ROUTE_DESCRIPTION}}   -   Tổng km: {{TOTAL_KILOMETERS}}
                    Điều 3. Cước vận chuyển và phương thức thanh toán
                    - Cước vận chuyển: {{CONTRACT_VALUE_WITH_UNIT}} (Bằng chữ: {{CONTRACT_VALUE_WORDS}})
                    - Thanh toán: {{PAYMENT_DESCRIPTION}}
                    """,
                ResponsibilitiesSectionTitle = "III- TRÁCH NHIỆM CÁC BÊN",
                ResponsibilityHtxTitle = "BÊN HTX",
                ResponsibilityHtxBodyText = "- Cung cấp các dịch vụ liên quan đến thủ tục kinh doanh vận tải",
                ResponsibilityOwnerTitle = "BÊN CHỦ XE",
                ResponsibilityOwnerBodyText = """
                    - Trang bị đầy đủ và kiểm tra các giấy tờ kinh doanh vận tải, tem, logo, khẩu hiệu, trang thiết bị PCCC trước khi kinh doanh vận tải
                    - Cung cấp xe đúng yêu cầu về số lượng, chất lượng cho bên B. Nếu gặp sự cố bất thường phải thông báo ngay với bên B và cung cấp xe khác thay thế, chất lượng tương đương xe thuê.
                    - Giao hàng đúng giờ, đúng địa điểm, đúng khối lượng cho phép theo hợp đồng.
                    - Có quyền từ chối khi bên thuê yêu cầu chở chất cấm, chở quá khối lượng hoặc yêu cầu khác vi phạm luật giao thông và luật pháp Việt Nam.
                    - Chủ xe phải có trách nhiệm và nghĩa vụ thuế đối với nhà nước, chịu trách nhiệm quản lý đối với tài xế về mặt kinh doanh vận tải, đảm bảo đủ điều kiện lưu thông và vận chuyển hàng hoá - tuyệt đối không sử dụng chất cấm, chất kích thích.
                    - Chủ xe phải có trách nhiệm thực hiện đúng và đủ Luật giao thông đường bộ, các hành vi vi phạm luật an toàn giao thông bên chủ xe phải hoàn toàn chịu trách nhiệm. Thông báo đến HTX khi có các thay đổi về nhân sự hoặc sự cố an toàn giao thông.
                    """,
                ResponsibilityCustomerTitle = "BÊN B",
                ResponsibilityCustomerBodyText = """
                    - Giao đúng số lượng theo quy định, không giao hàng cấm trong kiện hàng, thùng hàng.
                    - Cung cấp đúng và đầy đủ thông tin hàng hoá trên xe. Tuân thủ theo sự hướng dẫn của tài xế nhằm đảm bảo An toàn giao thông
                    - Thanh toán đúng số tiền và thời gian thanh toán cam kết trên hợp đồng
                    - Thông tin Liên hệ phản ánh khi cần: {{COMPANY_COMPLAINT_CONTACT}}
                    """,
                GeneralCommitmentText = """
                    Các bên cam kết thực hiện đúng các nội dung đã ghi trên Hợp đồng. Các vướng mắc sẽ giải quyết trên tinh thần hoà giải hợp tác.
                    Nếu không thoả thuận được sẽ được đưa ra toà án có thẩm quyền giải quyết
                    """,
                Page1SigLeftTitle = "ĐẠI DIỆN HTX",
                Page1SigCenterTitle = "CHỦ XE",
                Page1SigRightTitle = "ĐẠI DIỆN BÊN A",
                Page2LeftHeaderLine1 = "{{HEADER_COMPANY_LICENSE_ISSUED_PLACE}}",
                Page2LeftHeaderLine2 = "{{HEADER_COMPANY_OFFICE_NAME}}",
                Page2Title = "GIẤY VẬN TẢI",
                Page2Subtitle = """
                    Số HĐ: {{TRANSPORT_CONTRACT_NUMBER}}   -   Có giá trị đến: {{TRANSPORT_VALID_UNTIL}}
                    Biển kiểm soát xe: {{TRANSPORT_VEHICLE_PLATE}}
                    """,
                Page2FooterSummaryLine = string.Empty,
                Page2SigLeftTitle = "ĐẠI DIỆN BÊN A",
                Page2SigLeftSubTitle = string.Empty,
                Page2SigRightTitle = "TÀI XẾ",
                Page2SigRightSubTitle = string.Empty,
                UploadedDocxFileName = "Mau_HD_Chinh_HangHoa.docx"
            };
        }

        return new MainContractTemplateDto
        {
            BusinessType = ContractBusinessType.Passenger,
            TemplateName = "Mẫu Hợp đồng Vận chuyển Hành khách (Convert từ PDF gốc 2 trang)",
            LeftHeaderLine1 = string.Empty,
            LeftHeaderLine2 = string.Empty,
            DocumentTitle = "HỢP ĐỒNG VẬN CHUYỂN HÀNH KHÁCH",
            ContractNumberLine = "Số: {{CONTRACT_NUMBER}}/HĐVC-HTX",
            LegalBasesText = """
                - Căn cứ Bộ luật dân sự năm 2015;
                - Căn cứ vào sự thỏa thuận của hai bên.
                """,
            IntroDateLocationText = """
                Hôm nay vào lúc {{CONTRACT_TIME}} ngày {{CONTRACT_DAY}} tháng {{CONTRACT_MONTH}} năm {{CONTRACT_YEAR}} tại Văn phòng {{COMPANY_OFFICE_NAME}}
                chúng tôi gồm có:
                """,
            PartyASectionTitle = "I- ĐẠI DIỆN BÊN A: (Bên vận tải)",
            PartyABodyText = """
                1- Doanh nghiệp: {{COMPANY_NAME}} (MST: {{COMPANY_TAX_CODE}} - Số GPKDVT: {{COMPANY_LICENSE}})
                2- Địa chỉ trụ sở: {{COMPANY_ADDRESS}} - Điện thoại: {{COMPANY_PHONE}}
                3- Người đại diện: {{COMPANY_REPRESENTATIVE}} - CCCD: {{COMPANY_REP_CITIZEN_ID}} - Ngày cấp: {{COMPANY_REP_ISSUED_DATE}}
                4- Chủ phương tiện: {{OWNER_NAME}} - CCCD: {{OWNER_CITIZEN_ID}} - Ngày cấp: {{OWNER_ISSUED_DATE}}
                """,
            PartyBSectionTitle = "II- ĐẠI DIỆN BÊN B: (Bên thuê vận tải)",
            PartyBBodyText = """
                1- Tên cơ quan hoặc người thuê xe: {{CUSTOMER_NAME}} - MST: {{CUSTOMER_TAX_CODE}}
                2- Địa chỉ: {{CUSTOMER_ADDRESS}} - SĐT: {{CUSTOMER_PHONE}}
                3- Người đại diện: {{CUSTOMER_REPRESENTATIVE}} - CCCD: {{CUSTOMER_CITIZEN_ID}} - Ngày cấp: {{CUSTOMER_ISSUED_DATE}}
                """,
            TransportDetailsTitle = "Hai bên cùng thống nhất ký hợp đồng vận chuyển khách cụ thể như sau:",
            TransportDetailsBodyText = """
                - Xe khách biển số: {{VEHICLE_PLATE}} - Nhãn hiệu: {{VEHICLE_BRAND_MODEL}}
                - Sức chứa: {{SEAT_COUNT}} - Số lượng khách đi trên xe: {{PASSENGER_COUNT_2D}} (có danh sách kèm theo)
                - Họ và tên lái xe 1: {{DRIVER_NAME}}   hạng GPLX: {{DRIVER_LICENSE_CLASS}}
                - Họ và tên lái xe 2: {{SECOND_DRIVER_NAME}}   hạng GPLX: {{SECOND_DRIVER_LICENSE_CLASS}}
                - Thời gian địa điểm đón khách: {{PICKUP_DATETIME_LOCATION}}
                - Thời gian địa điểm trả khách: {{DROPOFF_DATETIME_LOCATION}}
                - Hành trình xe chạy: {{ROUTE_DESCRIPTION}} - Tổng km: {{TOTAL_KILOMETERS}}
                - Giá cước vận tải: {{CONTRACT_VALUE_WITH_UNIT}} (Bằng chữ: {{CONTRACT_VALUE_WORDS}})
                - Hình thức thanh toán: {{PAYMENT_METHOD}} - Thời gian thanh toán: {{PAYMENT_TIME}}
                """,
            ResponsibilitiesSectionTitle = "III- TRÁCH NHIỆM CÁC BÊN",
            ResponsibilityHtxTitle = "BÊN HTX",
            ResponsibilityHtxBodyText = "- Cung cấp các dịch vụ liên quan đến thủ tục kinh doanh vận tải",
            ResponsibilityOwnerTitle = "BÊN CHỦ XE",
            ResponsibilityOwnerBodyText = """
                - Trang bị đầy đủ và kiểm tra các giấy tờ kinh doanh vận tải, tem, logo, khẩu hiệu, trang thiết bị PCCC trước khi kinh doanh vận tải
                - Cung cấp xe đúng yêu cầu về số lượng, chất lượng cho bên B. Nếu gặp sự cố bất thường phải thông báo ngay với bên B và cung cấp xe khác thay thế, chất lượng tương đương xe thuê.
                - Đưa đón đúng giờ, đúng địa điểm theo hợp đồng.
                - Có quyền từ chối khi hành khách mang theo chất cấm hoặc yêu cầu khác vi phạm luật giao thông và luật pháp Việt Nam.
                - Chủ xe phải có trách nhiệm và nghĩa vụ thuế đối với nhà nước, chịu trách nhiệm quản lý đối với tài xế về mặt kinh doanh vận tải, đảm bảo đủ điều kiện lưu thông và vận chuyển hành khách - tuyệt đối không sử dụng chất cấm.
                - Chủ xe phải có trách nhiệm thực hiện đúng và đủ Luật giao thông đường bộ; các hành vi vi phạm luật an toàn giao thông bên chủ xe phải hoàn toàn chịu trách nhiệm. Thông báo đến HTX khi có các thay đổi về nhân sự hoặc sự cố an toàn giao thông.
                """,
            ResponsibilityCustomerTitle = "BÊN B",
            ResponsibilityCustomerBodyText = """
                - Đi đúng số người theo quy định, không mang hàng cấm trong hành lý.
                - Cung cấp đúng và đầy đủ thông tin hành khách đi trên xe. Tuân thủ theo sự hướng dẫn của tài xế nhằm đảm bảo An toàn giao thông.
                - Tự quản lý số lượng người và tài sản đi trên xe.
                - Thanh toán đúng số tiền và thời gian thanh toán cam kết trên hợp đồng.
                - Thông tin Liên hệ phản ánh khi cần: {{COMPANY_COMPLAINT_CONTACT}}
                """,
            GeneralCommitmentText = """
                Các bên cam kết thực hiện đúng các nội dung đã ghi trên Hợp đồng. Các vướng mắc sẽ giải quyết trên tinh thần hoà giải hợp tác.
                Nếu không thoả thuận được sẽ được đưa ra toà án có thẩm quyền giải quyết.
                """,
            Page1SigLeftTitle = "ĐẠI DIỆN HTX",
            Page1SigCenterTitle = "CHỦ XE",
            Page1SigRightTitle = "ĐẠI DIỆN BÊN B",
            Page2LeftHeaderLine1 = "{{HEADER_COMPANY_OFFICE_NAME}}",
            Page2LeftHeaderLine2 = string.Empty,
            Page2Title = "DANH SÁCH HÀNH KHÁCH",
            Page2Subtitle = "(Kèm theo hợp đồng vận chuyển số: {{CONTRACT_NUMBER}}/HĐVC-HTX ngày {{CONTRACT_DAY}} tháng {{CONTRACT_MONTH}} năm {{CONTRACT_YEAR}})",
            Page2FooterSummaryLine = "TỔNG SỐ: {{PASSENGER_COUNT_2D}} Người",
            Page2SigLeftTitle = "BÊN B",
            Page2SigLeftSubTitle = "Ký tên, đóng dấu (nếu có)",
            Page2SigRightTitle = "TÀI XẾ",
            Page2SigRightSubTitle = "Ký và ghi rõ họ tên",
            UploadedDocxFileName = "Mau_HD_Chinh_HanhKhach.docx"
        };
    }

    private static Table BuildPassengerListWordTable(Func<string?, string> R, MainContractTemplateDto template)
    {
        var table = new Table(
            new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                CreateSingleTableBorders()));

        var headerRow = new TableRow();
        headerRow.Append(
            CreateBorderedCell("STT", widthPct: "450", bold: true, center: true),
            CreateBorderedCell("Họ tên hành khách", widthPct: "2350", bold: true, center: true),
            CreateBorderedCell("Năm sinh", widthPct: "850", bold: true, center: true),
            CreateBorderedCell("Ghi chú", widthPct: "1350", bold: true, center: true));
        table.Append(headerRow);

        for (var i = 1; i <= 20; i++)
        {
            var row = new TableRow();
            row.Append(
                CreateBorderedCell(i.ToString(CultureInfo.InvariantCulture), widthPct: "450", bold: false, center: true),
                CreateBorderedCell(R($"{{{{P{i:00}_NAME}}}}"), widthPct: "2350", bold: false, center: false),
                CreateBorderedCell(R($"{{{{P{i:00}_BIRTH_YEAR}}}}"), widthPct: "850", bold: false, center: true),
                CreateBorderedCell(R($"{{{{P{i:00}_NOTE}}}}"), widthPct: "1350", bold: false, center: false));
            table.Append(row);
        }

        var footerRow = new TableRow();
        footerRow.Append(
            CreateBorderedCell(string.Empty, widthPct: "450", bold: true, center: true),
            CreateBorderedCell(R(template.Page2FooterSummaryLine), widthPct: "2350", bold: true, center: true),
            CreateBorderedCell(string.Empty, widthPct: "850", bold: false, center: true),
            CreateBorderedCell(string.Empty, widthPct: "1350", bold: false, center: false));
        table.Append(footerRow);

        return table;
    }

    private static Table BuildCargoTransportPaperWordTable(Func<string?, string> R)
    {
        var table = new Table(
            new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                CreateSingleTableBorders()));

        // 1. & 2.
        var r1Head = new TableRow();
        r1Head.Append(
            CreateBorderedCell("1. Thông tin về đơn vị kinh doanh", widthPct: "2500", bold: true, center: false),
            CreateBorderedCell("2. Thông tin về người lái xe", widthPct: "2500", bold: true, center: false));
        table.Append(r1Head);

        var r1Body = new TableRow();
        r1Body.Append(
            CreateBorderedMultiLineCell(
                [
                    R("Đơn vị vận tải: {{TRANSPORT_COMPANY_NAME}}"),
                    R("(MST: {{TRANSPORT_COMPANY_TAX_CODE}} - Số GPKDVT: {{TRANSPORT_COMPANY_LICENSE}})"),
                    R("Địa chỉ trụ sở: {{TRANSPORT_COMPANY_ADDRESS}}")
                ],
                widthPct: "2500"),
            CreateBorderedMultiLineCell(
                [
                    R("TX1: {{TRANSPORT_DRIVER1_NAME}}"),
                    R("Giấy phép lái xe số: {{TRANSPORT_DRIVER1_LICENSE_NUMBER}}"),
                    R("TX2: {{TRANSPORT_DRIVER2_NAME}}"),
                    R("Giấy phép lái xe số: {{TRANSPORT_DRIVER2_LICENSE_NUMBER}}")
                ],
                widthPct: "2500"));
        table.Append(r1Body);

        // 3. & 4.
        var r2Head = new TableRow();
        r2Head.Append(
            CreateBorderedCell("3. Thông tin về chuyến đi", widthPct: "2500", bold: true, center: false),
            CreateBorderedCell("4. Thông tin về hàng hóa", widthPct: "2500", bold: true, center: false));
        table.Append(r2Head);

        var r2Body = new TableRow();
        r2Body.Append(
            CreateBorderedMultiLineCell(
                [
                    R("Người đại diện: {{TRANSPORT_COMPANY_REPRESENTATIVE}}"),
                    R("CCCD: {{TRANSPORT_COMPANY_REP_CITIZEN_ID}}"),
                    R("Số điện thoại liên hệ: {{TRANSPORT_COMPANY_PHONE}}"),
                    R("Tuyến vận chuyển: {{TRANSPORT_ROUTE}}"),
                    R("Điểm xếp hàng: {{TRANSPORT_LOADING_POINT}}"),
                    R("Điểm giao hàng: {{TRANSPORT_DELIVERY_POINT}}")
                ],
                widthPct: "2500"),
            CreateBorderedMultiLineCell(
                [
                    R("Tên hàng hóa: {{TRANSPORT_GOODS_NAME}}"),
                    R("Khối lượng hàng hóa: {{TRANSPORT_CARGO_WEIGHT}}"),
                    R("Thông tin khác: {{TRANSPORT_OTHER_INFO}}")
                ],
                widthPct: "2500"));
        table.Append(r2Body);

        // 5. Xếp / dỡ hàng hóa
        var r3Head = new TableRow();
        r3Head.Append(
            CreateBorderedCell("5. Phần dành cho người xếp, dỡ hàng hóa lên, xuống xe — Thông tin về xếp hàng lên xe", widthPct: "2500", bold: true, center: false),
            CreateBorderedCell("Thông tin về dỡ hàng lên xe", widthPct: "2500", bold: true, center: false));
        table.Append(r3Head);

        var loadLines = new List<string>();
        var unloadLines = new List<string>();
        for (var i = 1; i <= 3; i++)
        {
            loadLines.Add(R($"- Xếp lần {i}: Địa điểm: {{{{LOAD{i}_LOCATION}}}}"));
            loadLines.Add(R($"- Khối lượng hàng: {{{{LOAD{i}_WEIGHT}}}}   -   Thời gian: {{{{LOAD{i}_TIME}}}}"));
            loadLines.Add(R($"- Xác nhận của người xếp hàng: {{{{LOAD{i}_CONFIRMATION}}}}"));

            unloadLines.Add(R($"- Dỡ lần {i}: Địa điểm: {{{{UNLOAD{i}_LOCATION}}}}"));
            unloadLines.Add(R($"- Khối lượng hàng: {{{{UNLOAD{i}_WEIGHT}}}}   -   Thời gian: {{{{UNLOAD{i}_TIME}}}}"));
            unloadLines.Add(R($"- Xác nhận của người dỡ hàng: {{{{UNLOAD{i}_CONFIRMATION}}}}"));
        }

        var r3Body = new TableRow();
        r3Body.Append(
            CreateBorderedMultiLineCell(loadLines, widthPct: "2500"),
            CreateBorderedMultiLineCell(unloadLines, widthPct: "2500"));
        table.Append(r3Body);

        return table;
    }

    private static void ParseParagraphsIntoTemplate(IReadOnlyList<string> paragraphs, MainContractTemplateDto target)
    {
        var legalLines = new List<string>();
        var partyALines = new List<string>();
        var partyBLines = new List<string>();
        var transportLines = new List<string>();
        var htxLines = new List<string>();
        var ownerLines = new List<string>();
        var customerRespLines = new List<string>();
        var commitLines = new List<string>();

        var section = "header";
        foreach (var raw in paragraphs)
        {
            var line = raw.Trim();
            if (line.StartsWith("- Căn cứ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Căn cứ", StringComparison.OrdinalIgnoreCase))
            {
                legalLines.Add(line);
                continue;
            }

            if (line.StartsWith("I- ĐẠI DIỆN BÊN A", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("BÊN A:", StringComparison.OrdinalIgnoreCase))
            {
                target.PartyASectionTitle = line;
                section = "partya";
                continue;
            }

            if (line.StartsWith("II- ĐẠI DIỆN BÊN B", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("BÊN B:", StringComparison.OrdinalIgnoreCase))
            {
                target.PartyBSectionTitle = line;
                section = "partyb";
                continue;
            }

            if (line.StartsWith("Hai bên cùng", StringComparison.OrdinalIgnoreCase))
            {
                target.TransportDetailsTitle = line;
                section = "transport";
                continue;
            }

            if (line.StartsWith("III- TRÁCH NHIỆM", StringComparison.OrdinalIgnoreCase))
            {
                target.ResponsibilitiesSectionTitle = line;
                section = "resp";
                continue;
            }

            if (line.Equals("BÊN HTX", StringComparison.OrdinalIgnoreCase))
            {
                section = "resp_htx";
                continue;
            }

            if (line.Equals("BÊN CHỦ XE", StringComparison.OrdinalIgnoreCase))
            {
                section = "resp_owner";
                continue;
            }

            if (line.Equals("BÊN B", StringComparison.OrdinalIgnoreCase) && section.StartsWith("resp", StringComparison.Ordinal))
            {
                section = "resp_customer";
                continue;
            }

            if (line.StartsWith("Các bên cam kết", StringComparison.OrdinalIgnoreCase))
            {
                commitLines.Add(line);
                section = "commit";
                continue;
            }

            if (line.Contains("ĐẠI DIỆN HTX", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("TRANG 2/2", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            switch (section)
            {
                case "partya":
                    partyALines.Add(line);
                    break;
                case "partyb":
                    partyBLines.Add(line);
                    break;
                case "transport":
                    transportLines.Add(line);
                    break;
                case "resp_htx":
                    htxLines.Add(line);
                    break;
                case "resp_owner":
                    ownerLines.Add(line);
                    break;
                case "resp_customer":
                    customerRespLines.Add(line);
                    break;
                case "commit":
                    commitLines.Add(line);
                    break;
            }
        }

        if (legalLines.Count > 0)
            target.LegalBasesText = string.Join(Environment.NewLine, legalLines);
        if (partyALines.Count > 0)
            target.PartyABodyText = string.Join(Environment.NewLine, partyALines);
        if (partyBLines.Count > 0)
            target.PartyBBodyText = string.Join(Environment.NewLine, partyBLines);
        if (transportLines.Count > 0)
            target.TransportDetailsBodyText = string.Join(Environment.NewLine, transportLines);
        if (htxLines.Count > 0)
            target.ResponsibilityHtxBodyText = string.Join(Environment.NewLine, htxLines);
        if (ownerLines.Count > 0)
            target.ResponsibilityOwnerBodyText = string.Join(Environment.NewLine, ownerLines);
        if (customerRespLines.Count > 0)
            target.ResponsibilityCustomerBodyText = string.Join(Environment.NewLine, customerRespLines);
        if (commitLines.Count > 0)
            target.GeneralCommitmentText = string.Join(Environment.NewLine, commitLines);
    }

    private MainContractTemplateBundleDto LoadBundle()
    {
        var path = GetTemplatesJsonPath();
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<MainContractTemplateBundleDto>(json, ReadJsonOptions);
                if (loaded is not null)
                {
                    loaded.DefaultPassenger ??= CreateDefaultPreset(ContractBusinessType.Passenger);
                    loaded.DefaultCargo ??= CreateDefaultPreset(ContractBusinessType.Cargo);
                    loaded.CompanyPassengerTemplates ??= new(StringComparer.OrdinalIgnoreCase);
                    loaded.CompanyCargoTemplates ??= new(StringComparer.OrdinalIgnoreCase);
                    return loaded;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không thể đọc file MainContractTemplates.json, sử dụng mẫu mặc định.");
            }
        }

        return new MainContractTemplateBundleDto
        {
            Version = 1,
            DefaultPassenger = CreateDefaultPreset(ContractBusinessType.Passenger),
            DefaultCargo = CreateDefaultPreset(ContractBusinessType.Cargo),
            CompanyPassengerTemplates = new(StringComparer.OrdinalIgnoreCase),
            CompanyCargoTemplates = new(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static async Task PersistBundleAsync(
        string path,
        MainContractTemplateBundleDto bundle,
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

    private static TableBorders CreateSingleTableBorders()
        => new(
            new TopBorder { Val = BorderValues.Single, Size = 6, Color = "333333" },
            new BottomBorder { Val = BorderValues.Single, Size = 6, Color = "333333" },
            new LeftBorder { Val = BorderValues.Single, Size = 6, Color = "333333" },
            new RightBorder { Val = BorderValues.Single, Size = 6, Color = "333333" },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "333333" },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "333333" });

    private static Table CreateBorderlessTwoColumnTable(
        IEnumerable<(string Text, bool Bold, bool Italic, string FontSizeHalfPt)> leftLines,
        IEnumerable<(string Text, bool Bold, bool Italic, string FontSizeHalfPt)> rightLines)
    {
        var table = new Table(
            new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None })));

        var row = new TableRow();
        row.Append(
            CreateBorderlessCell(leftLines, "2500"),
            CreateBorderlessCell(rightLines, "2500"));
        table.Append(row);
        return table;
    }

    private static Table CreateBorderlessThreeColumnTable(
        IEnumerable<(string Text, bool Bold, bool Italic, string FontSizeHalfPt)> col1Lines,
        IEnumerable<(string Text, bool Bold, bool Italic, string FontSizeHalfPt)> col2Lines,
        IEnumerable<(string Text, bool Bold, bool Italic, string FontSizeHalfPt)> col3Lines)
    {
        var table = new Table(
            new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None })));

        var row = new TableRow();
        row.Append(
            CreateBorderlessCell(col1Lines, "1666"),
            CreateBorderlessCell(col2Lines, "1666"),
            CreateBorderlessCell(col3Lines, "1668"));
        table.Append(row);
        return table;
    }

    private static TableCell CreateBorderlessCell(
        IEnumerable<(string Text, bool Bold, bool Italic, string FontSizeHalfPt)> lines,
        string widthPct)
    {
        var cell = new TableCell(
            new TableCellProperties(
                new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = widthPct }));

        foreach (var (text, bold, italic, fontSize) in lines)
        {
            var runProps = CreateTimesRunProps(bold, italic, fontSize);
            var paraProps = new ParagraphProperties(
                new SpacingBetweenLines { After = "30", Line = "240", LineRule = LineSpacingRuleValues.Auto },
                new Justification { Val = JustificationValues.Center });

            cell.Append(new Paragraph(
                paraProps,
                new Run(runProps, new Text(text ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve })));
        }

        return cell;
    }

    private static TableCell CreateBorderedCell(
        string text,
        string widthPct,
        bool bold,
        bool center)
    {
        var cell = new TableCell(
            new TableCellProperties(
                new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = widthPct },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                new TableCellMargin(
                    new TopMargin { Width = "45", Type = TableWidthUnitValues.Dxa },
                    new BottomMargin { Width = "45", Type = TableWidthUnitValues.Dxa },
                    new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                    new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa })));

        var runProps = CreateTimesRunProps(bold, italic: false, "20");
        var paraProps = new ParagraphProperties(
            new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto },
            new Justification { Val = center ? JustificationValues.Center : JustificationValues.Left });

        cell.Append(new Paragraph(
            paraProps,
            new Run(runProps, new Text(text ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve })));
        return cell;
    }

    private static TableCell CreateBorderedMultiLineCell(
        IEnumerable<string> lines,
        string widthPct)
    {
        var cell = new TableCell(
            new TableCellProperties(
                new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = widthPct },
                new TableCellMargin(
                    new TopMargin { Width = "55", Type = TableWidthUnitValues.Dxa },
                    new BottomMargin { Width = "55", Type = TableWidthUnitValues.Dxa },
                    new LeftMargin { Width = "90", Type = TableWidthUnitValues.Dxa },
                    new RightMargin { Width = "90", Type = TableWidthUnitValues.Dxa })));

        foreach (var line in lines)
        {
            var runProps = CreateTimesRunProps(bold: false, italic: false, "19");
            var paraProps = new ParagraphProperties(
                new SpacingBetweenLines { After = "25", Line = "240", LineRule = LineSpacingRuleValues.Auto },
                new Justification { Val = JustificationValues.Left });

            cell.Append(new Paragraph(
                paraProps,
                new Run(runProps, new Text(line ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve })));
        }

        return cell;
    }

    private static void AppendMultiLineBlock(
        Body body,
        string text,
        bool bold,
        bool italic,
        string fontSizeHalfPt = "21",
        bool center = false)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var lines = text.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var isHeading = trimmed.StartsWith("Điều ", StringComparison.OrdinalIgnoreCase);
            AppendParagraph(body, trimmed, bold: bold || isHeading, italic: italic, center: center, fontSizeHalfPt: fontSizeHalfPt, spaceAfter: "45");
        }
    }

    private static void AppendParagraph(
        Body body,
        string text,
        bool bold,
        bool italic,
        bool center = false,
        bool rightAlign = false,
        string fontSizeHalfPt = "21",
        string spaceAfter = "50")
    {
        var runProps = CreateTimesRunProps(bold, italic, fontSizeHalfPt);
        var justification = rightAlign
            ? JustificationValues.Right
            : center
                ? JustificationValues.Center
                : JustificationValues.Both;

        var paraProps = new ParagraphProperties(
            new SpacingBetweenLines { After = spaceAfter, Line = "260", LineRule = LineSpacingRuleValues.Auto },
            new Justification { Val = justification });

        body.Append(new Paragraph(
            paraProps,
            new Run(runProps, new Text(text ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve })));
    }

    private static RunProperties CreateTimesRunProps(bool bold, bool italic, string fontSizeHalfPt)
    {
        var runProps = new RunProperties(
            new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", ComplexScript = "Times New Roman" },
            new FontSize { Val = fontSizeHalfPt },
            new FontSizeComplexScript { Val = fontSizeHalfPt });

        if (bold)
            runProps.Append(new Bold());
        if (italic)
            runProps.Append(new Italic());

        return runProps;
    }

    private static async Task EnsureOwnerAccessAsync(AuthenticationStateProvider authStateProvider)
    {
        var auth = await authStateProvider.GetAuthenticationStateAsync();
        var user = auth.User;
        if (user.Identity?.IsAuthenticated == true && user.IsInRole("Owner"))
            return;

        throw new UnauthorizedAccessException("Chỉ tài khoản Owner mới có quyền chỉnh sửa Mẫu Hợp đồng Chính.");
    }

    private static MainContractTemplateDto Clone(MainContractTemplateDto source)
        => JsonSerializer.Deserialize<MainContractTemplateDto>(
            JsonSerializer.Serialize(source, WriteJsonOptions),
            ReadJsonOptions)!;

    private static DateTime VietnamTime(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value.AddHours(7) : value;

    private static string FormatDateOnly(DateTime? value)
        => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "...";

    private static string FormatDateTime(DateTime? value)
        => value is null ? "..." : VietnamTime(value.Value).ToString("dd/MM/yyyy HH:mm");

    private static string BuildDateTimeLocation(DateTime? value, string? location)
    {
        var dt = value is null ? "..." : VietnamTime(value.Value).ToString("dd/MM/yyyy HH:mm");
        return $"{dt} - {First(location, "...")}";
    }

    private static string JoinNonEmpty(string separator, params string?[] values)
    {
        var parts = values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).ToArray();
        return parts.Length == 0 ? "..." : string.Join(separator, parts);
    }

    private static string First(params string?[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;

    private static string Capitalize(string value)
        => string.IsNullOrWhiteSpace(value)
            ? value
            : char.ToUpper(value[0], CultureInfo.GetCultureInfo("vi-VN")) + value[1..];

    private static string NumberToVietnameseWords(decimal? amount)
    {
        if (amount is null)
            return "chưa xác định";

        var number = (long)Math.Round(amount.Value, 0, MidpointRounding.AwayFromZero);
        if (number == 0)
            return "không";
        if (number < 0)
            return $"âm {ReadPositiveNumber(-number)}";
        return ReadPositiveNumber(number);
    }

    private static string ReadPositiveNumber(long number)
    {
        string[] units = ["", "nghìn", "triệu", "tỷ", "nghìn tỷ", "triệu tỷ"];
        var groups = new List<int>();
        while (number > 0)
        {
            groups.Add((int)(number % 1000));
            number /= 1000;
        }

        var parts = new List<string>();
        for (var index = groups.Count - 1; index >= 0; index--)
        {
            var group = groups[index];
            if (group == 0)
                continue;

            var full = index < groups.Count - 1 && group < 100;
            var words = ReadThreeDigits(group, full);
            if (!string.IsNullOrWhiteSpace(words))
                parts.Add(string.IsNullOrEmpty(units[index]) ? words : $"{words} {units[index]}");
        }

        return string.Join(" ", parts);
    }

    private static string ReadThreeDigits(int number, bool full)
    {
        string[] digit = ["không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"];
        var hundreds = number / 100;
        var tens = (number % 100) / 10;
        var ones = number % 10;
        var parts = new List<string>();

        if (hundreds > 0 || full)
        {
            parts.Add($"{digit[hundreds]} trăm");
            if (tens == 0 && ones > 0)
                parts.Add("lẻ");
        }

        if (tens > 1)
        {
            parts.Add($"{digit[tens]} mươi");
            if (ones == 1) parts.Add("mốt");
            else if (ones == 4) parts.Add("tư");
            else if (ones == 5) parts.Add("lăm");
            else if (ones > 0) parts.Add(digit[ones]);
        }
        else if (tens == 1)
        {
            parts.Add("mười");
            if (ones == 5) parts.Add("lăm");
            else if (ones > 0) parts.Add(digit[ones]);
        }
        else if (ones > 0)
        {
            parts.Add(digit[ones]);
        }

        return string.Join(" ", parts);
    }
}
