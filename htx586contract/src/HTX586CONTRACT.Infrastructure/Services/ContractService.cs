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

/// <summary>
/// Luồng hợp đồng tinh gọn: chỉ Driver tạo/cập nhật. Công ty lấy từ Admin được
/// gán cho Driver; xe/chủ xe và khách hàng được nhập trực tiếp rồi chụp snapshot.
/// </summary>
public sealed class ContractService(
    IDbContextFactory<ApplicationDbContext> factory,
    UserManager<ApplicationUser> userManager) : IContractService
{
    public async Task<IReadOnlyList<ContractListItemDto>> GetAsync(ContractFilter filter, CancellationToken ct = default)
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

        if (filter.Status == ContractStatus.Completed)
        {
            query = query.Where(x => x.Status == ContractStatus.Completed &&
                                     !string.IsNullOrEmpty(x.PdfFileUrl) &&
                                     !string.IsNullOrEmpty(x.PdfSha256) &&
                                     x.PdfGeneratedAt.HasValue);
        }
        else if (filter.Status == ContractStatus.WaitingCustomerSignature)
        {
            // Gộp trạng thái Draft cũ và các bản Completed lỗi chưa tạo được PDF
            // về đúng trạng thái nghiệp vụ hiện tại: chờ xác nhận từ khách hàng.
            query = query.Where(x =>
                (x.Status is ContractStatus.Draft or ContractStatus.WaitingCustomerSignature) ||
                (x.Status == ContractStatus.Completed &&
                 (string.IsNullOrEmpty(x.PdfFileUrl) ||
                  string.IsNullOrEmpty(x.PdfSha256) ||
                  !x.PdfGeneratedAt.HasValue)));
        }
        else if (filter.Status.HasValue)
        {
            query = query.Where(x => x.Status == filter.Status.Value);
        }
        if (filter.BusinessType.HasValue) query = query.Where(x => x.BusinessType == filter.BusinessType.Value);
        if (!string.IsNullOrWhiteSpace(filter.DriverId)) query = query.Where(x => x.DriverId == filter.DriverId);
        if (!string.IsNullOrWhiteSpace(filter.AdminId)) query = query.Where(x => x.AdminId == filter.AdminId);
        if (filter.CompanyProfileId.HasValue) query = query.Where(x => x.CompanyProfileId == filter.CompanyProfileId.Value);
        if (filter.FromDate.HasValue) query = query.Where(x => (x.StartTime ?? x.CreatedAt) >= filter.FromDate.Value.Date);
        if (filter.ToDate.HasValue) query = query.Where(x => (x.StartTime ?? x.CreatedAt) < filter.ToDate.Value.Date.AddDays(1));

        return await query.OrderByDescending(x => x.CreatedAt)
            .Select(x => new ContractListItemDto
            {
                Id = x.Id,
                ContractNumber = x.ContractNumber,
                BusinessType = x.BusinessType,
                CompanyName = x.CompanyNameSnapshot,
                CustomerName = x.CustomerNameSnapshot,
                DriverName = x.DriverNameSnapshot,
                VehicleId = x.VehicleId,
                VehiclePlate = x.VehiclePlateSnapshot,
                StartTime = x.StartTime,
                ContractValue = x.ContractValue,
                Status = x.Status == ContractStatus.Completed &&
                         (!string.IsNullOrEmpty(x.PdfFileUrl) &&
                          !string.IsNullOrEmpty(x.PdfSha256) &&
                          x.PdfGeneratedAt.HasValue)
                    ? ContractStatus.Completed
                    : x.Status == ContractStatus.Completed
                        ? ContractStatus.WaitingCustomerSignature
                        : x.Status,
                IsFinalized = x.Status == ContractStatus.Completed &&
                              !string.IsNullOrEmpty(x.PdfFileUrl) &&
                              !string.IsNullOrEmpty(x.PdfSha256) &&
                              x.PdfGeneratedAt.HasValue,
                PdfFileUrl = x.PdfFileUrl,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);
    }

    public Task<IReadOnlyList<ContractListItemDto>> GetDriverContractsAsync(string driverId, CancellationToken ct = default)
        => GetAsync(new ContractFilter { DriverId = driverId }, ct);

    public async Task<ContractDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var contract = await db.Contracts.AsNoTracking()
            .Include(x => x.Passengers)
            .Include(x => x.Signatures)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (contract is null) return null;

        var detail = new ContractDetailDto
        {
            Id = contract.Id,
            ContractNumber = contract.ContractNumber,
            BusinessType = contract.BusinessType,
            ContractTypeId = contract.ContractTypeId,
            Status = IsFinalized(contract)
                ? ContractStatus.Completed
                : contract.Status == ContractStatus.Completed
                    ? ContractStatus.WaitingCustomerSignature
                    : contract.Status,
            IsFinalized = IsFinalized(contract),
            AdminId = contract.AdminId,
            CompanyProfileId = contract.CompanyProfileId,
            CompanyName = contract.CompanyNameSnapshot,
            CompanyTaxCode = contract.CompanyTaxCodeSnapshot,
            CompanyAddress = contract.CompanyAddressSnapshot,
            CompanyRepresentativeName = contract.CompanyRepresentativeSnapshot,
            CompanyRepresentativePosition = contract.CompanyRepresentativePositionSnapshot,
            DriverId = contract.DriverId,
            DriverName = contract.DriverNameSnapshot,
            DriverLicenseNumber = contract.DriverLicenseNumberSnapshot,
            DriverLicenseClass = contract.DriverLicenseClassSnapshot,
            CustomerId = contract.CustomerId,
            CustomerName = contract.CustomerNameSnapshot,
            CustomerPhone = contract.CustomerPhoneSnapshot,
            CustomerCitizenId = contract.CustomerCitizenIdSnapshot,
            CustomerAddress = contract.CustomerAddressSnapshot,
            AreaCode = contract.AreaCode,
            VehicleId = contract.VehicleId,
            VehiclePlate = contract.VehiclePlateSnapshot,
            VehicleBrand = contract.VehicleBrandSnapshot,
            ActualPassengerCount = contract.ActualPassengerCount,
            OwnerName = contract.VehicleOwnerNameSnapshot,
            OwnerCitizenId = contract.VehicleOwnerCitizenIdSnapshot,
            CargoName = contract.CargoName,
            CargoWeight = contract.CargoWeight,
            CargoUnit = contract.CargoUnit,
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
            Passengers = contract.Passengers.OrderBy(x => x.SortOrder).Select(x => new ContractPassengerDto
            {
                Id = x.Id,
                SortOrder = x.SortOrder,
                FullName = x.FullName,
                BirthYear = x.BirthYear,
                Note = x.Note
            }).ToList(),
            Signatures = contract.Signatures.OrderBy(x => x.ServerSignedAt).Select(x => new ContractSignatureDto
            {
                Id = x.Id,
                Party = x.Party,
                SignerName = x.SignerName,
                SignatureFileUrl = x.SignatureFileUrl,
                ServerSignedAt = x.ServerSignedAt
            }).ToList()
        };

        var snapshot = ContractSnapshotData.FromJson(contract.ContractDataJson);
        if (snapshot is not null)
        {
            ApplySnapshot(detail, snapshot);
            // Snapshot là nguồn dữ liệu chính của luồng tinh gọn. Luôn lấy danh sách
            // hành khách từ cùng một dòng Contracts để không phụ thuộc bảng phụ cũ.
            detail.Passengers = snapshot.Passengers
                .OrderBy(x => x.SortOrder)
                .Select(x => new ContractPassengerDto
                {
                    Id = Guid.Empty,
                    SortOrder = x.SortOrder,
                    FullName = x.FullName ?? string.Empty,
                    BirthYear = x.BirthYear,
                    Note = x.Note
                })
                .ToList();
            detail.ActualPassengerCount = detail.Passengers.Count(x => !string.IsNullOrWhiteSpace(x.FullName));
        }

        detail.CreatedByName = await GetUserDisplayNameAsync(db, detail.CreatedByUserId, ct);
        return detail;
    }

    public async Task<SaveContractResult> CreateAsync(SaveContractRequest request, string currentUserId, CancellationToken ct = default)
    {
        if (request.BusinessType != ContractBusinessType.Passenger)
            return new(false, null, "Hiện chỉ sử dụng hợp đồng vận chuyển hành khách.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var driver = await LoadActiveDriverAsync(db, currentUserId, ct);
        if (driver is null)
            return new(false, null, "Không tìm thấy tài khoản Driver đang hoạt động hoặc yêu cầu đăng ký chưa được duyệt.");

        var admin = await LoadAdminAsync(db, driver.AdminId, driver.CompanyProfileId, ct);
        if (admin is null)
            return new(false, null, "Tài xế chưa được gán tài khoản Admin/công ty đang hoạt động.");

        var validation = ValidateManualData(request);
        if (validation is not null) return new(false, null, validation);

        var type = await ResolveTypeAsync(db, request, ct);
        if (type is null) return new(false, null, "Chưa cấu hình loại hợp đồng vận chuyển hành khách.");
        var template = await db.ContractTemplates.FirstOrDefaultAsync(x => x.ContractTypeId == type.Id && x.IsActive, ct);
        if (template is null) return new(false, null, "Chưa cấu hình mẫu hợp đồng đang hoạt động.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            // Đếm toàn bộ lịch sử, kể cả hợp đồng đã hủy/ẩn, rồi +1.
            var contractSequence = await db.Contracts.IgnoreQueryFilters()
                .CountAsync(x => x.DriverId == driver.Id, ct) + 1;

            var entity = new Contract
            {
                Id = Guid.NewGuid(),
                ContractNumber = contractSequence.ToString(),
                BusinessType = ContractBusinessType.Passenger,
                ContractTypeId = type.Id,
                ContractTemplateId = template.Id,
                AdminId = admin.Id,
                DriverId = driver.Id,
                CompanyProfileId = null,
                CustomerId = null,
                VehicleId = null,
                Status = ContractStatus.WaitingCustomerSignature,
                ContractContentSnapshot = template.HtmlContent,
                CreatedBy = currentUserId,
                CreatedAt = DateTime.UtcNow
            };

            ApplyBusinessData(entity, request);
            ApplySnapshots(entity, admin, driver, request);
            // Luồng mới lưu toàn bộ hành khách trực tiếp trong snapshot JSON của Contracts.
            // Không ghi thêm bảng ContractPassengers để tránh dữ liệu bị tách và lỗi concurrency.
            entity.ContractDataJson = BuildSnapshot(admin, driver, request).ToJson();

            db.Contracts.Add(entity);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(true, entity.Id, $"Đã lưu hợp đồng số {entity.ContractNumber}. Trạng thái: Chờ xác nhận từ khách hàng.");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<SaveContractResult> UpdateAsync(Guid id, SaveContractRequest request, string currentUserId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.Contracts
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (entity is null) return new(false, null, "Không tìm thấy hợp đồng.");
        if (!string.Equals(entity.DriverId, currentUserId, StringComparison.Ordinal))
            return new(false, id, "Chỉ tài xế tạo hợp đồng mới được cập nhật.");
        if (IsFinalized(entity) || entity.Status is ContractStatus.Cancelled or ContractStatus.Expired or ContractStatus.Invalidated)
            return new(false, id, "Hợp đồng đã tạo PDF và khóa, hoặc đã hủy nên không thể chỉnh sửa.");

        var driver = await LoadActiveDriverAsync(db, currentUserId, ct);
        if (driver is null) return new(false, id, "Tài khoản tài xế không còn hoạt động.");

        // Nếu đã có snapshot thì không cần đọc lại Admin cũ. Nhờ vậy hợp đồng nháp vẫn
        // chỉnh sửa được khi tài xế đổi công ty hoặc Admin cũ bị ngưng hoạt động.
        var existingSnapshot = ContractSnapshotData.FromJson(entity.ContractDataJson);
        ApplicationUser? admin = null;
        if (existingSnapshot is null)
        {
            var adminId = entity.AdminId ?? driver.AdminId;
            admin = await LoadAdminAsync(db, adminId, driver.CompanyProfileId, ct);
            if (admin is null) return new(false, id, "Không tìm thấy snapshot hoặc Admin/công ty của hợp đồng.");
        }

        var validation = ValidateManualData(request);
        if (validation is not null) return new(false, id, validation);

        ApplyBusinessData(entity, request);
        ApplyManualSnapshots(entity, request);
        // Hành khách được cập nhật trong chính ContractDataJson. Các dòng bảng phụ cũ
        // chỉ dùng để đọc hợp đồng legacy, không còn xóa/chèn lại khi lưu tạm.
        entity.ContractDataJson = existingSnapshot is not null
            ? BuildUpdatedSnapshot(existingSnapshot, request).ToJson()
            : BuildSnapshot(admin!, driver, request).ToJson();
        entity.Status = ContractStatus.WaitingCustomerSignature;
        entity.PdfFileUrl = null;
        entity.PdfSha256 = null;
        entity.PdfGeneratedAt = null;
        entity.ContractHash = null;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = currentUserId;

        await db.SaveChangesAsync(ct);
        return new(true, id, "Đã lưu hợp đồng. Trạng thái: Chờ xác nhận từ khách hàng. Có thể tiếp tục chỉnh sửa và ký lại.");
    }

    public async Task<SaveContractResult> CompleteAsync(Guid id, string currentUserId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var entity = await db.Contracts
            .Include(x => x.Signatures)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (entity is null) return new(false, null, "Không tìm thấy hợp đồng.");
        if (!string.Equals(entity.DriverId, currentUserId, StringComparison.Ordinal))
            return new(false, id, "Chỉ tài xế tạo hợp đồng mới được hoàn thành.");
        if (IsFinalized(entity))
            return new(true, id, "Hợp đồng đã hoàn thành, có PDF chính thức và đã bị khóa.");
        if (entity.Status is ContractStatus.Cancelled or ContractStatus.Expired or ContractStatus.Invalidated)
            return new(false, id, "Hợp đồng đã hủy, hết hạn hoặc vô hiệu hóa.");

        var snapshot = ContractSnapshotData.FromJson(entity.ContractDataJson);
        if (snapshot is null)
            return new(false, id, "Hợp đồng chưa có snapshot dữ liệu hợp lệ. Vui lòng lưu lại trước khi hoàn thành.");

        // Hỗ trợ hợp đồng nháp được tạo trước khi Admin/tài xế hoàn thiện chữ ký cố định.
        // Chỉ bù chữ ký, không thay đổi các thông tin snapshot khác của công ty/tài xế.
        var snapshotChanged = false;
        if (string.IsNullOrWhiteSpace(snapshot.Company.RepresentativeSignatureFileUrl))
        {
            var fixedAdminId = snapshot.Company.AdminId ?? entity.AdminId;
            if (!string.IsNullOrWhiteSpace(fixedAdminId))
            {
                var fixedAdmin = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == fixedAdminId, ct);
                if (fixedAdmin is not null)
                {
                    snapshot.Company.RepresentativeSignatureFileUrl = fixedAdmin.CompanySignatureFileUrl;
                    snapshot.Company.RepresentativeSignatureHash = fixedAdmin.CompanySignatureHash;
                    snapshot.Company.RepresentativeSignedAt = fixedAdmin.CompanySignedAt;
                    snapshotChanged = !string.IsNullOrWhiteSpace(snapshot.Company.RepresentativeSignatureFileUrl);
                }
            }
        }
        if (string.IsNullOrWhiteSpace(snapshot.Driver.SignatureFileUrl))
        {
            var fixedDriver = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == entity.DriverId, ct);
            if (fixedDriver is not null)
            {
                snapshot.Driver.SignatureFileUrl = fixedDriver.DriverSignatureFileUrl;
                snapshot.Driver.SignatureHash = fixedDriver.DriverSignatureHash;
                snapshot.Driver.SignedAt = fixedDriver.DriverSignedAt;
                snapshotChanged |= !string.IsNullOrWhiteSpace(snapshot.Driver.SignatureFileUrl);
            }
        }
        if (snapshotChanged)
            entity.ContractDataJson = snapshot.ToJson();

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(snapshot.Company.RepresentativeSignatureFileUrl)) missing.Add("chữ ký công ty");
        if (string.IsNullOrWhiteSpace(snapshot.Driver.SignatureFileUrl)) missing.Add("chữ ký tài xế");
        if (string.IsNullOrWhiteSpace(snapshot.Vehicle.OwnerSignatureFileUrl) &&
            !entity.Signatures.Any(x => x.Party == SignatureParty.VehicleOwner &&
                                        !string.IsNullOrWhiteSpace(x.SignatureFileUrl)))
            missing.Add("chữ ký chủ sở hữu xe");
        if (string.IsNullOrWhiteSpace(snapshot.Customer.SignatureFileUrl) &&
            !entity.Signatures.Any(x => x.Party == SignatureParty.Customer &&
                                        !string.IsNullOrWhiteSpace(x.SignatureFileUrl)))
            missing.Add("chữ ký khách hàng");

        if (missing.Count > 0)
            return new(false, id, $"Chưa thể hoàn thành. Còn thiếu: {string.Join(", ", missing)}.");

        if (string.IsNullOrWhiteSpace(entity.PdfFileUrl) ||
            string.IsNullOrWhiteSpace(entity.PdfSha256) ||
            !entity.PdfGeneratedAt.HasValue)
        {
            return new(false, id,
                "Chưa thể khóa hợp đồng vì file PDF chính thức chưa được tạo thành công.");
        }

        var now = DateTime.UtcNow;
        entity.Status = ContractStatus.Completed;
        entity.CompletedAt = now;
        entity.UpdatedAt = now;
        entity.UpdatedBy = currentUserId;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(true, id, "Đã tạo PDF, hoàn thành và khóa hợp đồng.");
    }

    public async Task<SaveContractResult> CancelByDriverAsync(Guid id, string currentUserId, string? reason = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.Contracts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return new(false, null, "Không tìm thấy hợp đồng.");
        if (!string.Equals(entity.DriverId, currentUserId, StringComparison.Ordinal))
            return new(false, id, "Bạn không có quyền hủy hợp đồng này.");
        if (IsFinalized(entity))
            return new(false, id, "Hợp đồng đã hoàn thành, có PDF chính thức và bị khóa.");
        if (entity.Status == ContractStatus.Cancelled)
            return new(false, id, "Hợp đồng đã được hủy.");

        entity.Status = ContractStatus.Cancelled;
        entity.CancelledAt = DateTime.UtcNow;
        entity.CancelReason = N(reason) ?? "Tài xế hủy hợp đồng.";
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = currentUserId;
        await db.SaveChangesAsync(ct);
        return new(true, id, "Đã hủy hợp đồng.");
    }

    public async Task<bool> DeleteAsync(Guid id, string currentUserId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.Contracts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null || IsFinalized(entity)) return false;
        if (!string.Equals(entity.DriverId, currentUserId, StringComparison.Ordinal)) return false;

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedBy = currentUserId;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static bool IsFinalized(Contract entity)
        => entity.Status == ContractStatus.Completed &&
           !string.IsNullOrWhiteSpace(entity.PdfFileUrl) &&
           !string.IsNullOrWhiteSpace(entity.PdfSha256) &&
           entity.PdfGeneratedAt.HasValue;

    private async Task<ApplicationUser?> LoadActiveDriverAsync(ApplicationDbContext db, string userId, CancellationToken ct)
    {
        var driver = await db.Users.FirstOrDefaultAsync(x =>
            x.Id == userId && x.IsActive && !x.IsDeleted && x.RegistrationStatus == "Approved", ct);
        if (driver is null || !await userManager.IsInRoleAsync(driver, "Driver")) return null;
        return driver;
    }

    private async Task<ApplicationUser?> LoadAdminAsync(ApplicationDbContext db, string? adminId, Guid? legacyCompanyId, CancellationToken ct)
    {
        ApplicationUser? admin = null;
        if (!string.IsNullOrWhiteSpace(adminId))
            admin = await db.Users.FirstOrDefaultAsync(x => x.Id == adminId && x.IsActive && !x.IsDeleted, ct);

        // Tương thích dữ liệu cũ: tìm Admin đang gắn CompanyProfile của tài xế.
        if (admin is null && legacyCompanyId.HasValue)
        {
            admin = await (from user in db.Users
                           join userRole in db.UserRoles on user.Id equals userRole.UserId
                           join role in db.Roles on userRole.RoleId equals role.Id
                           where user.CompanyProfileId == legacyCompanyId && role.Name == "Admin" && user.IsActive && !user.IsDeleted
                           select user).FirstOrDefaultAsync(ct);
        }

        if (admin is null || !await userManager.IsInRoleAsync(admin, "Admin")) return null;
        return admin;
    }

    private static string? ValidateManualData(SaveContractRequest request)
    {
        // Lưu hợp đồng được phép để trống toàn bộ dữ liệu nhập tay. Chỉ giới hạn
        // số dòng hành khách theo khả năng hiển thị của mẫu PDF hiện tại.
        if (request.Passengers.Count(x => !string.IsNullOrWhiteSpace(x.FullName)) > 20)
            return "Danh sách hành khách tối đa 20 người theo mẫu PDF hiện tại.";
        return null;
    }

    private static async Task<ContractType?> ResolveTypeAsync(ApplicationDbContext db, SaveContractRequest request, CancellationToken ct)
    {
        if (request.ContractTypeId.HasValue)
        {
            var selected = await db.ContractTypes.FirstOrDefaultAsync(x => x.Id == request.ContractTypeId && x.IsActive, ct);
            if (selected is not null) return selected;
        }
        return await db.ContractTypes.FirstOrDefaultAsync(x => x.Code == "PASSENGER" && x.IsActive, ct);
    }

    private static void ApplyBusinessData(Contract entity, SaveContractRequest request)
    {
        entity.AreaCode = N(request.AreaCode) ?? "N/A";
        entity.CargoName = N(request.CargoName);
        entity.CargoWeight = request.CargoWeight;
        entity.CargoUnit = N(request.CargoUnit);
        entity.ActualPassengerCount = request.Passengers.Count(x => !string.IsNullOrWhiteSpace(x.FullName));
        entity.SecondDriverName = N(request.SecondDriverName);
        entity.SecondDriverLicenseClass = N(request.SecondDriverLicenseClass);
        entity.PickupLocation = N(request.PickupLocation);
        entity.DropoffLocation = N(request.DropoffLocation);
        entity.StartTime = request.StartTime;
        entity.EndTime = request.EndTime;
        entity.RouteDescription = N(request.RouteDescription);
        entity.TotalKilometers = request.TotalKilometers;
        entity.ContractValue = request.ContractValue;
        entity.PaymentMethod = N(request.PaymentMethod);
        entity.PaymentTime = N(request.PaymentTime);
        entity.Note = N(request.Note);
    }

    private static void ApplySnapshots(Contract entity, ApplicationUser admin, ApplicationUser driver, SaveContractRequest request)
    {
        entity.AdminId = admin.Id;
        entity.DriverId = driver.Id;
        entity.CompanyNameSnapshot = CompanyDisplayName(admin);
        entity.CompanyTaxCodeSnapshot = N(admin.CompanyTaxCode) ?? string.Empty;
        entity.CompanyAddressSnapshot = N(admin.CompanyAddress) ?? string.Empty;
        entity.CompanyRepresentativeSnapshot = N(admin.CompanyRepresentativeName) ?? admin.FullName;
        entity.CompanyRepresentativePositionSnapshot = N(admin.CompanyRepresentativePosition);
        entity.DriverNameSnapshot = driver.FullName;
        entity.DriverLicenseNumberSnapshot = N(driver.DriverLicenseNumber);
        entity.DriverLicenseClassSnapshot = N(driver.DriverLicenseClass);
        ApplyManualSnapshots(entity, request);
    }

    private static void ApplyManualSnapshots(Contract entity, SaveContractRequest request)
    {
        entity.CustomerNameSnapshot = N(request.CustomerName) ?? string.Empty;
        entity.CustomerPhoneSnapshot = N(request.CustomerPhone) ?? string.Empty;
        entity.CustomerCitizenIdSnapshot = N(request.CustomerCitizenId);
        entity.CustomerAddressSnapshot = N(request.CustomerAddress);
        entity.VehiclePlateSnapshot = request.VehiclePlate?.Trim().ToUpperInvariant();
        entity.VehicleBrandSnapshot = N(request.VehicleBrand);
        entity.VehicleOwnerNameSnapshot = request.OwnerName?.Trim();
        entity.VehicleOwnerCitizenIdSnapshot = N(request.OwnerCitizenId);
    }

    private static ContractSnapshotData BuildSnapshot(ApplicationUser admin, ApplicationUser driver, SaveContractRequest request)
        => ContractSnapshotData.CaptureManual(
            admin,
            driver,
            new CustomerSnapshot
            {
                FullName = N(request.CustomerName),
                OrganizationName = N(request.CustomerOrganizationName),
                TaxCode = N(request.CustomerTaxCode),
                PhoneNumber = N(request.CustomerPhone),
                CitizenId = N(request.CustomerCitizenId),
                CitizenIdIssuedDate = request.CustomerCitizenIdIssuedDate,
                CitizenIdIssuedPlace = N(request.CustomerCitizenIdIssuedPlace),
                Address = N(request.CustomerAddress),
                Email = N(request.CustomerEmail)
            },
            new VehicleSnapshot
            {
                PlateNumber = request.VehiclePlate?.Trim().ToUpperInvariant(),
                VehicleCode = N(request.VehicleCode),
                Brand = N(request.VehicleBrand),
                Model = N(request.VehicleModel),
                VehicleType = N(request.VehicleType),
                SeatCount = request.SeatCount,
                Color = N(request.VehicleColor),
                ChassisNumber = N(request.ChassisNumber),
                EngineNumber = N(request.EngineNumber),
                OwnerName = N(request.OwnerName),
                OwnerCitizenId = N(request.OwnerCitizenId),
                OwnerCitizenIdIssuedDate = request.OwnerCitizenIdIssuedDate,
                OwnerCitizenIdIssuedPlace = N(request.OwnerCitizenIdIssuedPlace),
                OwnerAddress = N(request.OwnerAddress),
                OwnerPhoneNumber = N(request.OwnerPhoneNumber)
            },
            request.Passengers
                .Where(x => !string.IsNullOrWhiteSpace(x.FullName))
                .Select((x, index) => new PassengerSnapshot
                {
                    SortOrder = index + 1,
                    FullName = x.FullName.Trim(),
                    BirthYear = x.BirthYear,
                    Note = N(x.Note)
                }));

    private static ContractSnapshotData BuildUpdatedSnapshot(
        ContractSnapshotData existing,
        SaveContractRequest request)
    {
        var updated = new ContractSnapshotData
        {
            Version = Math.Max(existing.Version, 3),
            CapturedAtUtc = existing.CapturedAtUtc,
            Company = existing.Company,
            Driver = existing.Driver,
            Customer = new CustomerSnapshot
            {
                FullName = N(request.CustomerName),
                OrganizationName = N(request.CustomerOrganizationName),
                TaxCode = N(request.CustomerTaxCode),
                PhoneNumber = N(request.CustomerPhone),
                CitizenId = N(request.CustomerCitizenId),
                CitizenIdIssuedDate = request.CustomerCitizenIdIssuedDate,
                CitizenIdIssuedPlace = N(request.CustomerCitizenIdIssuedPlace),
                Address = N(request.CustomerAddress),
                Email = N(request.CustomerEmail),
                SignatureFileUrl = existing.Customer.SignatureFileUrl,
                SignatureHash = existing.Customer.SignatureHash,
                SignedAt = existing.Customer.SignedAt
            },
            Vehicle = new VehicleSnapshot
            {
                PlateNumber = request.VehiclePlate?.Trim().ToUpperInvariant(),
                VehicleCode = N(request.VehicleCode),
                Brand = N(request.VehicleBrand),
                Model = N(request.VehicleModel),
                VehicleType = N(request.VehicleType),
                SeatCount = request.SeatCount,
                Color = N(request.VehicleColor),
                ChassisNumber = N(request.ChassisNumber),
                EngineNumber = N(request.EngineNumber),
                OwnerName = N(request.OwnerName),
                OwnerCitizenId = N(request.OwnerCitizenId),
                OwnerCitizenIdIssuedDate = request.OwnerCitizenIdIssuedDate,
                OwnerCitizenIdIssuedPlace = N(request.OwnerCitizenIdIssuedPlace),
                OwnerAddress = N(request.OwnerAddress),
                OwnerPhoneNumber = N(request.OwnerPhoneNumber),
                OwnerSignatureFileUrl = existing.Vehicle.OwnerSignatureFileUrl,
                OwnerSignatureHash = existing.Vehicle.OwnerSignatureHash,
                OwnerSignedAt = existing.Vehicle.OwnerSignedAt
            },
            Passengers = request.Passengers
                .Where(x => !string.IsNullOrWhiteSpace(x.FullName))
                .Select((x, index) => new PassengerSnapshot
                {
                    SortOrder = index + 1,
                    FullName = x.FullName.Trim(),
                    BirthYear = x.BirthYear,
                    Note = N(x.Note)
                })
                .ToList()
        };
        return updated;
    }

    private static void AddPassengers(Contract entity, IEnumerable<ContractPassengerDto> passengers, string userId)
    {
        foreach (var item in passengers.Where(x => !string.IsNullOrWhiteSpace(x.FullName)).Select((value, index) => (value, index)))
        {
            entity.Passengers.Add(new ContractPassenger
            {
                SortOrder = item.index + 1,
                FullName = item.value.FullName.Trim(),
                BirthYear = item.value.BirthYear,
                Note = N(item.value.Note),
                CreatedBy = userId
            });
        }
    }

    private static void ApplySnapshot(ContractDetailDto detail, ContractSnapshotData snapshot)
    {
        detail.AdminId = snapshot.Company.AdminId ?? detail.AdminId;
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
        AddSnapshotSignature(detail, SignatureParty.VehicleOwner, snapshot.Vehicle.OwnerName,
            snapshot.Vehicle.OwnerSignatureFileUrl, snapshot.Vehicle.OwnerSignedAt, snapshot.CapturedAtUtc);
        AddSnapshotSignature(detail, SignatureParty.Customer, snapshot.Customer.FullName,
            snapshot.Customer.SignatureFileUrl, snapshot.Customer.SignedAt, snapshot.CapturedAtUtc);
        detail.VehicleOwnerSignatureFileUrl = detail.Signatures.FirstOrDefault(x => x.Party == SignatureParty.VehicleOwner)?.SignatureFileUrl;
        detail.VehicleOwnerSignedAt = detail.Signatures.FirstOrDefault(x => x.Party == SignatureParty.VehicleOwner)?.ServerSignedAt;
    }

    private static void AddSnapshotSignature(
        ContractDetailDto detail,
        SignatureParty party,
        string? signerName,
        string? signatureFileUrl,
        DateTime? signedAt,
        DateTime fallbackTime)
    {
        if (string.IsNullOrWhiteSpace(signatureFileUrl) || detail.Signatures.Any(x => x.Party == party)) return;
        detail.Signatures.Add(new ContractSignatureDto
        {
            Id = Guid.Empty,
            Party = party,
            SignerName = N(signerName) ?? string.Empty,
            SignatureFileUrl = signatureFileUrl,
            ServerSignedAt = signedAt ?? fallbackTime
        });
    }

    private static async Task<string> GetUserDisplayNameAsync(ApplicationDbContext db, string? userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) return "Hệ thống";
        return await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.FullName).FirstOrDefaultAsync(ct) ?? userId;
    }

    private static string CompanyDisplayName(ApplicationUser admin)
        => string.IsNullOrWhiteSpace(admin.CompanyBranchName)
            ? N(admin.CompanyName) ?? admin.FullName
            : $"{N(admin.CompanyName) ?? admin.FullName} - {admin.CompanyBranchName.Trim()}";

    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
