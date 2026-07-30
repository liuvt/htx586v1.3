using System.Data;
using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Application.Contracts;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Enums;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Infrastructure.Services;

public sealed class ContractService(
    IDbContextFactory<ApplicationDbContext> factory,
    UserManager<ApplicationUser> userManager) : IContractService
{
    public async Task<IReadOnlyList<ContractListItemDto>> GetAsync(
        ContractFilter filter,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.Contracts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x =>
                x.ContractNumber.Contains(search) ||
                x.CustomerNameSnapshot.Contains(search) ||
                x.DriverNameSnapshot.Contains(search) ||
                (x.VehiclePlateSnapshot != null && x.VehiclePlateSnapshot.Contains(search)));
        }

        if (filter.Status.HasValue) query = query.Where(x => x.Status == filter.Status.Value);
        if (!string.IsNullOrWhiteSpace(filter.DriverId)) query = query.Where(x => x.DriverId == filter.DriverId);
        if (!string.IsNullOrWhiteSpace(filter.AdminId)) query = query.Where(x => x.AdminId == filter.AdminId);
        if (filter.FromDate.HasValue) query = query.Where(x => (x.StartTime ?? x.CreatedAt) >= filter.FromDate.Value.Date);
        if (filter.ToDate.HasValue) query = query.Where(x => (x.StartTime ?? x.CreatedAt) < filter.ToDate.Value.Date.AddDays(1));

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ContractListItemDto
            {
                Id = x.Id,
                ContractNumber = x.ContractNumber,
                CompanyName = x.CompanyNameSnapshot,
                CustomerName = x.CustomerNameSnapshot,
                DriverName = x.DriverNameSnapshot,
                VehiclePlate = x.VehiclePlateSnapshot,
                StartTime = x.StartTime,
                ContractValue = x.ContractValue,
                Status = x.Status,
                IsFinalized = x.Status == ContractStatus.Completed &&
                              x.PdfFileUrl != null &&
                              x.PdfSha256 != null &&
                              x.PdfGeneratedAt.HasValue,
                PdfFileUrl = x.PdfFileUrl,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);
    }

    public Task<IReadOnlyList<ContractListItemDto>> GetDriverContractsAsync(
        string driverId,
        CancellationToken ct = default)
        => GetAsync(new ContractFilter { DriverId = driverId }, ct);

    public async Task<ContractDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var contract = await db.Contracts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (contract is null) return null;

        var snapshot = ContractSnapshotData.FromJson(contract.ContractDataJson);
        if (snapshot is null) return null;

        var detail = new ContractDetailDto
        {
            Id = contract.Id,
            ContractNumber = contract.ContractNumber,
            Status = contract.Status,
            IsFinalized = IsFinalized(contract),
            AdminId = contract.AdminId,
            DriverId = contract.DriverId,
            AreaCode = contract.AreaCode,
            ActualPassengerCount = contract.ActualPassengerCount,
            SecondDriverName = contract.SecondDriverName,
            SecondDriverLicenseClass = contract.SecondDriverLicenseClass,
            PickupLocation = contract.PickupLocation,
            DropoffLocation = contract.DropoffLocation,
            StartTime = contract.StartTime,
            EndTime = contract.EndTime,
            RouteDescription = contract.RouteDescription,
            TotalKilometers = contract.TotalKilometers,
            ContractValue = contract.ContractValue,
            PaymentMethod = contract.PaymentMethod,
            PaymentTime = contract.PaymentTime,
            Note = contract.Note,
            PdfFileUrl = contract.PdfFileUrl,
            CreatedAt = contract.CreatedAt,
            CreatedByUserId = contract.CreatedBy,
            CreatedByName = await GetUserDisplayNameAsync(db, contract.CreatedBy, ct)
        };

        ApplySnapshot(detail, snapshot);
        return detail;
    }

    public async Task<SaveContractResult> CreateAsync(
        SaveContractRequest request,
        string currentUserId,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var driver = await LoadActiveDriverAsync(db, currentUserId, ct);
        if (driver is null)
            return new(false, null, "Không tìm thấy tài khoản Driver đang hoạt động hoặc yêu cầu đăng ký chưa được duyệt.");

        var admin = await LoadAdminAsync(db, driver.AdminId, ct);
        if (admin is null)
            return new(false, null, "Tài xế chưa được gán tài khoản Admin/công ty đang hoạt động.");

        var validation = Validate(request);
        if (validation is not null) return new(false, null, validation);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var sequence = await db.Contracts.IgnoreQueryFilters()
                .CountAsync(x => x.DriverId == driver.Id, ct) + 1;

            var snapshot = BuildSnapshot(admin, driver, request);
            var entity = new Contract
            {
                Id = Guid.NewGuid(),
                ContractNumber = sequence.ToString(),
                Status = ContractStatus.WaitingCustomerSignature,
                AdminId = admin.Id,
                DriverId = driver.Id,
                ContractDataJson = snapshot.ToJson(),
                CreatedBy = currentUserId,
                CreatedAt = DateTime.UtcNow
            };

            ApplyBusinessData(entity, request);
            ApplySearchSnapshots(entity, snapshot);

            db.Contracts.Add(entity);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(true, entity.Id,
                $"Đã lưu hợp đồng số {entity.ContractNumber}. Trạng thái: Chờ xác nhận từ khách hàng.");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<SaveContractResult> UpdateAsync(
        Guid id,
        SaveContractRequest request,
        string currentUserId,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.Contracts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return new(false, null, "Không tìm thấy hợp đồng.");
        if (!string.Equals(entity.DriverId, currentUserId, StringComparison.Ordinal))
            return new(false, id, "Chỉ tài xế tạo hợp đồng mới được cập nhật.");
        if (entity.Status != ContractStatus.WaitingCustomerSignature || IsFinalized(entity))
            return new(false, id, "Hợp đồng đã hoàn thành nên không thể chỉnh sửa.");

        var validation = Validate(request);
        if (validation is not null) return new(false, id, validation);

        var existing = ContractSnapshotData.FromJson(entity.ContractDataJson);
        if (existing is null)
            return new(false, id, "Dữ liệu snapshot hợp đồng không hợp lệ.");

        var updated = BuildUpdatedSnapshot(existing, request);
        entity.ContractDataJson = updated.ToJson();
        entity.Status = ContractStatus.WaitingCustomerSignature;
        entity.PdfFileUrl = null;
        entity.PdfSha256 = null;
        entity.PdfGeneratedAt = null;
        entity.ContractHash = null;
        entity.CompletedAt = null;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = currentUserId;
        ApplyBusinessData(entity, request);
        ApplySearchSnapshots(entity, updated);

        await db.SaveChangesAsync(ct);
        return new(true, id,
            "Đã lưu hợp đồng. Trạng thái: Chờ xác nhận từ khách hàng. Có thể tiếp tục chỉnh sửa và ký lại.");
    }

    private async Task<ApplicationUser?> LoadActiveDriverAsync(
        ApplicationDbContext db,
        string userId,
        CancellationToken ct)
    {
        var driver = await db.Users.FirstOrDefaultAsync(x =>
            x.Id == userId && x.IsActive && !x.IsDeleted && x.RegistrationStatus == "Approved", ct);
        return driver is not null && await userManager.IsInRoleAsync(driver, "Driver") ? driver : null;
    }

    private async Task<ApplicationUser?> LoadAdminAsync(
        ApplicationDbContext db,
        string? adminId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(adminId)) return null;
        var admin = await db.Users.FirstOrDefaultAsync(x =>
            x.Id == adminId && x.IsActive && !x.IsDeleted, ct);
        return admin is not null && await userManager.IsInRoleAsync(admin, "Admin") ? admin : null;
    }

    private static string? Validate(SaveContractRequest request)
        => request.Passengers.Count(x => !string.IsNullOrWhiteSpace(x.FullName)) > 20
            ? "Danh sách hành khách tối đa 20 người theo mẫu PDF hiện tại."
            : null;

    private static void ApplyBusinessData(Contract entity, SaveContractRequest request)
    {
        entity.AreaCode = Normalize(request.AreaCode) ?? string.Empty;
        entity.ActualPassengerCount = request.Passengers.Count(x => !string.IsNullOrWhiteSpace(x.FullName));
        entity.SecondDriverName = Normalize(request.SecondDriverName);
        entity.SecondDriverLicenseClass = Normalize(request.SecondDriverLicenseClass);
        entity.PickupLocation = Normalize(request.PickupLocation);
        entity.DropoffLocation = Normalize(request.DropoffLocation);
        entity.StartTime = request.StartTime;
        entity.EndTime = request.EndTime;
        entity.RouteDescription = Normalize(request.RouteDescription);
        entity.TotalKilometers = request.TotalKilometers;
        entity.ContractValue = request.ContractValue;
        entity.PaymentMethod = Normalize(request.PaymentMethod);
        entity.PaymentTime = Normalize(request.PaymentTime);
        entity.Note = Normalize(request.Note);
    }

    private static void ApplySearchSnapshots(Contract entity, ContractSnapshotData snapshot)
    {
        entity.CompanyNameSnapshot = snapshot.Company.DisplayName;
        entity.DriverNameSnapshot = snapshot.Driver.FullName ?? string.Empty;
        entity.CustomerNameSnapshot = snapshot.Customer.FullName ?? string.Empty;
        entity.CustomerPhoneSnapshot = snapshot.Customer.PhoneNumber ?? string.Empty;
        entity.VehiclePlateSnapshot = snapshot.Vehicle.PlateNumber;
        entity.VehicleOwnerNameSnapshot = snapshot.Vehicle.OwnerName;
    }

    private static ContractSnapshotData BuildSnapshot(
        ApplicationUser admin,
        ApplicationUser driver,
        SaveContractRequest request)
        => ContractSnapshotData.Capture(
            admin,
            driver,
            BuildCustomer(request),
            BuildVehicle(request),
            BuildPassengers(request));

    private static ContractSnapshotData BuildUpdatedSnapshot(
        ContractSnapshotData existing,
        SaveContractRequest request)
        => new()
        {
            Version = existing.Version,
            CapturedAtUtc = existing.CapturedAtUtc,
            Company = existing.Company,
            Driver = existing.Driver,
            Customer = BuildCustomer(request, existing.Customer),
            Vehicle = BuildVehicle(request, existing.Vehicle),
            Passengers = BuildPassengers(request).ToList()
        };

    private static CustomerSnapshot BuildCustomer(
        SaveContractRequest request,
        CustomerSnapshot? existing = null)
        => new()
        {
            FullName = Normalize(request.CustomerName),
            OrganizationName = Normalize(request.CustomerOrganizationName),
            TaxCode = Normalize(request.CustomerTaxCode),
            PhoneNumber = Normalize(request.CustomerPhone),
            CitizenId = Normalize(request.CustomerCitizenId),
            CitizenIdIssuedDate = request.CustomerCitizenIdIssuedDate,
            CitizenIdIssuedPlace = Normalize(request.CustomerCitizenIdIssuedPlace),
            Address = Normalize(request.CustomerAddress),
            Email = Normalize(request.CustomerEmail),
            SignatureFileUrl = existing?.SignatureFileUrl,
            SignatureHash = existing?.SignatureHash,
            SignedAt = existing?.SignedAt
        };

    private static VehicleSnapshot BuildVehicle(
        SaveContractRequest request,
        VehicleSnapshot? existing = null)
        => new()
        {
            PlateNumber = request.VehiclePlate?.Trim().ToUpperInvariant(),
            VehicleCode = Normalize(request.VehicleCode),
            Brand = Normalize(request.VehicleBrand),
            Model = Normalize(request.VehicleModel),
            VehicleType = Normalize(request.VehicleType),
            SeatCount = request.SeatCount,
            Color = Normalize(request.VehicleColor),
            ChassisNumber = Normalize(request.ChassisNumber),
            EngineNumber = Normalize(request.EngineNumber),
            OwnerName = Normalize(request.OwnerName),
            OwnerCitizenId = Normalize(request.OwnerCitizenId),
            OwnerCitizenIdIssuedDate = request.OwnerCitizenIdIssuedDate,
            OwnerCitizenIdIssuedPlace = Normalize(request.OwnerCitizenIdIssuedPlace),
            OwnerAddress = Normalize(request.OwnerAddress),
            OwnerPhoneNumber = Normalize(request.OwnerPhoneNumber),
            OwnerSignatureFileUrl = existing?.OwnerSignatureFileUrl,
            OwnerSignatureHash = existing?.OwnerSignatureHash,
            OwnerSignedAt = existing?.OwnerSignedAt
        };

    private static IEnumerable<PassengerSnapshot> BuildPassengers(SaveContractRequest request)
        => request.Passengers
            .Where(x => !string.IsNullOrWhiteSpace(x.FullName))
            .Select((x, index) => new PassengerSnapshot
            {
                SortOrder = index + 1,
                FullName = x.FullName.Trim(),
                BirthYear = x.BirthYear,
                Note = Normalize(x.Note)
            });

    private static void ApplySnapshot(ContractDetailDto detail, ContractSnapshotData snapshot)
    {
        detail.CompanyName = snapshot.Company.DisplayName;
        detail.CompanyTaxCode = snapshot.Company.TaxCode;
        detail.CompanyAddress = snapshot.Company.Address;
        detail.CompanyPhone = snapshot.Company.PhoneNumber;
        detail.CompanyRepresentativeName = snapshot.Company.RepresentativeName;
        detail.CompanyRepresentativePosition = snapshot.Company.RepresentativePosition;
        detail.CompanyRepresentativeSignatureFileUrl = snapshot.Company.RepresentativeSignatureFileUrl;
        detail.CompanyRepresentativeSignedAt = snapshot.Company.RepresentativeSignedAt;

        detail.DriverName = snapshot.Driver.FullName ?? string.Empty;
        detail.DriverPhone = snapshot.Driver.PhoneNumber;
        detail.DriverCitizenId = snapshot.Driver.CitizenId;
        detail.DriverLicenseNumber = snapshot.Driver.DriverLicenseNumber;
        detail.DriverLicenseClass = snapshot.Driver.DriverLicenseClass;
        detail.DriverSignatureFileUrl = snapshot.Driver.SignatureFileUrl;
        detail.DriverSignedAt = snapshot.Driver.SignedAt;

        detail.CustomerName = snapshot.Customer.FullName ?? string.Empty;
        detail.CustomerPhone = snapshot.Customer.PhoneNumber ?? string.Empty;
        detail.CustomerCitizenId = snapshot.Customer.CitizenId;
        detail.CustomerCitizenIdIssuedDate = snapshot.Customer.CitizenIdIssuedDate;
        detail.CustomerCitizenIdIssuedPlace = snapshot.Customer.CitizenIdIssuedPlace;
        detail.CustomerAddress = snapshot.Customer.Address;
        detail.CustomerEmail = snapshot.Customer.Email;
        detail.CustomerOrganizationName = snapshot.Customer.OrganizationName;
        detail.CustomerTaxCode = snapshot.Customer.TaxCode;

        detail.VehiclePlate = snapshot.Vehicle.PlateNumber;
        detail.VehicleCode = snapshot.Vehicle.VehicleCode;
        detail.VehicleBrand = snapshot.Vehicle.Brand;
        detail.VehicleModel = snapshot.Vehicle.Model;
        detail.VehicleType = snapshot.Vehicle.VehicleType;
        detail.SeatCount = snapshot.Vehicle.SeatCount;
        detail.VehicleColor = snapshot.Vehicle.Color;
        detail.ChassisNumber = snapshot.Vehicle.ChassisNumber;
        detail.EngineNumber = snapshot.Vehicle.EngineNumber;
        detail.OwnerName = snapshot.Vehicle.OwnerName;
        detail.OwnerCitizenId = snapshot.Vehicle.OwnerCitizenId;
        detail.OwnerCitizenIdIssuedDate = snapshot.Vehicle.OwnerCitizenIdIssuedDate;
        detail.OwnerCitizenIdIssuedPlace = snapshot.Vehicle.OwnerCitizenIdIssuedPlace;
        detail.OwnerAddress = snapshot.Vehicle.OwnerAddress;
        detail.OwnerPhoneNumber = snapshot.Vehicle.OwnerPhoneNumber;
        detail.VehicleOwnerSignatureFileUrl = snapshot.Vehicle.OwnerSignatureFileUrl;
        detail.VehicleOwnerSignedAt = snapshot.Vehicle.OwnerSignedAt;

        detail.Passengers = snapshot.Passengers
            .OrderBy(x => x.SortOrder)
            .Select(x => new ContractPassengerDto
            {
                SortOrder = x.SortOrder,
                FullName = x.FullName,
                BirthYear = x.BirthYear,
                Note = x.Note
            })
            .ToList();

        AddSignature(detail, SignatureParty.VehicleOwner, snapshot.Vehicle.OwnerName,
            snapshot.Vehicle.OwnerSignatureFileUrl, snapshot.Vehicle.OwnerSignedAt);
        AddSignature(detail, SignatureParty.Customer, snapshot.Customer.FullName,
            snapshot.Customer.SignatureFileUrl, snapshot.Customer.SignedAt);
    }

    private static void AddSignature(
        ContractDetailDto detail,
        SignatureParty party,
        string? signerName,
        string? url,
        DateTime? signedAt)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        detail.Signatures.Add(new ContractSignatureDto
        {
            Party = party,
            SignerName = signerName ?? string.Empty,
            SignatureFileUrl = url,
            ServerSignedAt = signedAt ?? detail.CreatedAt
        });
    }

    private static bool IsFinalized(Contract entity)
        => entity.Status == ContractStatus.Completed &&
           !string.IsNullOrWhiteSpace(entity.PdfFileUrl) &&
           !string.IsNullOrWhiteSpace(entity.PdfSha256) &&
           entity.PdfGeneratedAt.HasValue;

    private static async Task<string> GetUserDisplayNameAsync(
        ApplicationDbContext db,
        string? userId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) return "Hệ thống";
        return await db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(ct) ?? userId;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
