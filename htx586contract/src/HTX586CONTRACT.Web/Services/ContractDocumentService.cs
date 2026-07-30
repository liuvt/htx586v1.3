using System.Data;
using System.Security.Cryptography;
using System.Text;
using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Enums;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Web.Services;

public sealed class ContractDocumentService(
    IDbContextFactory<ApplicationDbContext> factory,
    PdfContractTemplateRenderer pdfTemplateRenderer,
    IUploadFileStorage storage,
    ILogger<ContractDocumentService> logger) : IContractDocumentService
{
    public async Task<string> SaveSignatureAsync(
        Guid contractId,
        string currentUserId,
        SignatureParty party,
        string dataUrl,
        CancellationToken ct = default)
    {
        if (party is not (SignatureParty.Customer or SignatureParty.VehicleOwner))
            throw new InvalidOperationException("Chỉ chữ ký chủ sở hữu xe và khách hàng được ký tay trên hợp đồng.");

        StoredUploadFile? stored = null;
        string? oldUrl = null;
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var contract = await db.Contracts.FirstOrDefaultAsync(x => x.Id == contractId, ct)
                ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng.");

            if (!string.Equals(contract.DriverId, currentUserId, StringComparison.Ordinal))
                throw new UnauthorizedAccessException("Chỉ tài xế tạo hợp đồng mới được ghi nhận chữ ký.");
            if (contract.Status != ContractStatus.WaitingCustomerSignature)
                throw new InvalidOperationException("Hợp đồng đã hoàn thành nên không thể ký lại.");

            if (!await IsActiveDriverAsync(db, currentUserId, ct))
                throw new InvalidOperationException("Tài khoản Driver không còn hoạt động.");

            var snapshot = ContractSnapshotData.FromJson(contract.ContractDataJson)
                ?? throw new InvalidOperationException("Dữ liệu snapshot hợp đồng không hợp lệ.");

            stored = await storage.SaveImageDataUrlAsync(
                ["contracts", contractId.ToString("N"), "signatures"],
                party == SignatureParty.VehicleOwner ? "vehicle-owner" : "customer",
                dataUrl,
                ct);

            if (party == SignatureParty.VehicleOwner)
            {
                oldUrl = snapshot.Vehicle.OwnerSignatureFileUrl;
                snapshot.Vehicle.OwnerSignatureFileUrl = stored.RelativeUrl;
                snapshot.Vehicle.OwnerSignatureHash = stored.Sha256Hash;
                snapshot.Vehicle.OwnerSignedAt = stored.SavedAt;
            }
            else
            {
                oldUrl = snapshot.Customer.SignatureFileUrl;
                snapshot.Customer.SignatureFileUrl = stored.RelativeUrl;
                snapshot.Customer.SignatureHash = stored.Sha256Hash;
                snapshot.Customer.SignedAt = stored.SavedAt;
            }

            contract.ContractDataJson = snapshot.ToJson();
            contract.UpdatedAt = stored.SavedAt;
            contract.UpdatedBy = currentUserId;

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            if (!string.Equals(oldUrl, stored.RelativeUrl, StringComparison.OrdinalIgnoreCase))
                storage.DeleteIfExists(oldUrl);

            return stored.RelativeUrl;
        }
        catch (Exception ex)
        {
            if (stored is not null)
                storage.DeleteIfExists(stored.RelativeUrl);

            logger.LogError(ex,
                "Lưu chữ ký thất bại. ContractId={ContractId}, Party={Party}",
                contractId,
                party);
            throw;
        }
    }

    public async Task<string> CompleteAsync(
        Guid contractId,
        string currentUserId,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var contract = await db.Contracts.FirstOrDefaultAsync(x => x.Id == contractId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng.");

        if (!string.Equals(contract.DriverId, currentUserId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Chỉ tài xế tạo hợp đồng mới được hoàn thành.");

        if (contract.Status == ContractStatus.Completed)
        {
            if (!string.IsNullOrWhiteSpace(contract.PdfFileUrl) && storage.FileExists(contract.PdfFileUrl))
                return contract.PdfFileUrl;

            throw new InvalidOperationException("Hợp đồng đã hoàn thành nhưng file PDF không còn tồn tại.");
        }

        if (contract.Status != ContractStatus.WaitingCustomerSignature)
            throw new InvalidOperationException("Hợp đồng không ở trạng thái chờ xác nhận.");

        if (!await IsActiveDriverAsync(db, currentUserId, ct))
            throw new InvalidOperationException("Tài khoản Driver không còn hoạt động.");

        var snapshot = ContractSnapshotData.FromJson(contract.ContractDataJson)
            ?? throw new InvalidOperationException("Dữ liệu snapshot hợp đồng không hợp lệ.");

        var missing = MissingSignatures(snapshot);
        if (missing.Count > 0)
            throw new InvalidOperationException($"Chưa thể hoàn thành. Còn thiếu: {string.Join(", ", missing)}.");

        if (snapshot.Passengers.Count(x => !string.IsNullOrWhiteSpace(x.FullName)) > 20)
            throw new InvalidOperationException("Mẫu PDF chỉ hỗ trợ tối đa 20 hành khách.");

        var folder = new[] { "contracts", contractId.ToString("N"), "pdf" };
        var directory = storage.GetPhysicalDirectory(folder);
        Directory.CreateDirectory(directory);
        var fileName = $"hop-dong-{SafeFileName(contract.ContractNumber)}-{contractId:N}.pdf";
        var fullPath = Path.Combine(directory, fileName);
        var tempPath = Path.Combine(directory, $".{fileName}.generating");
        var relativeUrl = storage.BuildRelativeUrl(folder, fileName);
        var moved = false;

        try
        {
            TryDeleteFile(tempPath);
            await pdfTemplateRenderer.RenderPdfAsync(contract, tempPath, ct);

            var pdfBytes = await File.ReadAllBytesAsync(tempPath, ct);
            var now = DateTime.UtcNow;
            contract.PdfFileUrl = relativeUrl;
            contract.PdfSha256 = Convert.ToHexString(SHA256.HashData(pdfBytes));
            contract.ContractHash = CalculateContractHash(contract);
            contract.PdfGeneratedAt = now;
            contract.Status = ContractStatus.Completed;
            contract.CompletedAt = now;
            contract.UpdatedAt = now;
            contract.UpdatedBy = currentUserId;

            await db.SaveChangesAsync(ct);
            File.Move(tempPath, fullPath, overwrite: true);
            moved = true;
            await transaction.CommitAsync(ct);
            return relativeUrl;
        }
        catch (Exception ex)
        {
            TryDeleteFile(tempPath);
            if (moved) TryDeleteFile(fullPath);
            logger.LogError(ex, "Hoàn thành hợp đồng thất bại. ContractId={ContractId}", contractId);
            throw;
        }
    }

    private static Task<bool> IsActiveDriverAsync(
        ApplicationDbContext db,
        string userId,
        CancellationToken ct)
        => (from user in db.Users
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            where user.Id == userId &&
                  user.IsActive && !user.IsDeleted &&
                  user.RegistrationStatus == "Approved" &&
                  role.Name == "Driver"
            select user.Id).AnyAsync(ct);

    private List<string> MissingSignatures(ContractSnapshotData snapshot)
    {
        var missing = new List<string>();
        if (!storage.FileExists(snapshot.Company.RepresentativeSignatureFileUrl)) missing.Add("chữ ký công ty");
        if (!storage.FileExists(snapshot.Driver.SignatureFileUrl)) missing.Add("chữ ký tài xế");
        if (!storage.FileExists(snapshot.Vehicle.OwnerSignatureFileUrl)) missing.Add("chữ ký chủ sở hữu xe");
        if (!storage.FileExists(snapshot.Customer.SignatureFileUrl)) missing.Add("chữ ký khách hàng");
        return missing;
    }

    private static string CalculateContractHash(Contract contract)
    {
        var payload = string.Join("|",
            contract.Id,
            contract.ContractNumber,
            contract.AdminId,
            contract.DriverId,
            contract.StartTime?.ToString("O"),
            contract.EndTime?.ToString("O"),
            contract.PickupLocation,
            contract.DropoffLocation,
            contract.RouteDescription,
            contract.TotalKilometers,
            contract.ContractValue,
            contract.PaymentMethod,
            contract.PaymentTime,
            contract.Note,
            contract.ContractDataJson);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(c =>
            invalid.Contains(c) || !(char.IsLetterOrDigit(c) || c is '-' or '_' or '.') ? '-' : c).ToArray())
            .Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "hop-dong" : sanitized;
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }
}
