using HTX586CONTRACT.Web.Services;
using System.Security.Claims;

namespace HTX586CONTRACT.Web.Endpoints;

public static class ReportEndpoints
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static void MapReportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reports/export")
            .RequireAuthorization(policy => policy.RequireRole("Owner", "Admin"));

        group.MapGet("/contracts", ExportContractsAsync);
        group.MapGet("/drivers", ExportDriversAsync);
        group.MapGet("/vehicles", ExportVehiclesAsync);
        group.MapGet("/revenue", ExportRevenueAsync);
    }

    private static async Task<IResult> ExportContractsAsync(
        ClaimsPrincipal user,
        IExcelReportService reportService,
        CancellationToken cancellationToken)
    {
        return await ExportAsync(
            user,
            (userId, isOwner, ct) => reportService.ExportContractsAsync(userId, isOwner, ct),
            cancellationToken);
    }

    private static async Task<IResult> ExportDriversAsync(
        ClaimsPrincipal user,
        IExcelReportService reportService,
        CancellationToken cancellationToken)
    {
        return await ExportAsync(
            user,
            (userId, isOwner, ct) => reportService.ExportDriversAsync(userId, isOwner, ct),
            cancellationToken);
    }

    private static async Task<IResult> ExportVehiclesAsync(
        ClaimsPrincipal user,
        IExcelReportService reportService,
        CancellationToken cancellationToken)
    {
        return await ExportAsync(
            user,
            (userId, isOwner, ct) => reportService.ExportVehiclesAsync(userId, isOwner, ct),
            cancellationToken);
    }

    private static async Task<IResult> ExportRevenueAsync(
        ClaimsPrincipal user,
        IExcelReportService reportService,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken)
    {
        if (!fromDate.HasValue || !toDate.HasValue)
            return Results.BadRequest("Vui lòng chọn đầy đủ ngày bắt đầu và ngày kết thúc.");

        if (toDate.Value.Date < fromDate.Value.Date)
            return Results.BadRequest("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");

        if (toDate.Value.Date > fromDate.Value.Date.AddYears(1).AddDays(-1))
            return Results.BadRequest("Khoảng thời gian xuất doanh thu tối đa là 01 năm.");

        return await ExportAsync(
            user,
            (userId, isOwner, ct) => reportService.ExportRevenueAsync(
                userId,
                isOwner,
                fromDate.Value.Date,
                toDate.Value.Date,
                ct),
            cancellationToken);
    }

    private static async Task<IResult> ExportAsync(
        ClaimsPrincipal user,
        Func<string, bool, CancellationToken, Task<ExcelExportFile>> exportAction,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            var file = await exportAction(userId, user.IsInRole("Owner"), cancellationToken);
            return Results.File(file.Content, ExcelContentType, file.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }
}
