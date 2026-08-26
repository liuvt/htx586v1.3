using System.Security.Cryptography;
using System.Text;
using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Application.Contracts;
using HTX586CONTRACT.Domain.Companies;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Customers;
using HTX586CONTRACT.Domain.Enums;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Domain.Notifications;
using HTX586CONTRACT.Domain.Vehicles;
using HTX586CONTRACT.Infrastructure.Identity;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Infrastructure.Services;

/// <summary>
/// Luồng hợp đồng dùng ba role Owner, Admin và VehicleOwner.
/// Company/Văn phòng của VehicleOwner luôn được suy ra từ xe được chọn.
/// Completed và Cancelled là hai trạng thái khóa vĩnh viễn.
/// </summary>
public sealed class ContractService(
    IDbContextFactory<ApplicationDbContext> factory,
    SafeUserManager userManager) : IContractService
{
    public async Task<IReadOnlyList<ContractListItemDto>> GetAsync(
        ContractFilter filter,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.Contracts.AsNoTracking().Where(x => !x.IsDeleted).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x =>
                x.ContractNumber.Contains(search) ||
                x.CustomerNameSnapshot.Contains(search) ||
                x.DriverNameSnapshot.Contains(search) ||
                (x.VehiclePlateSnapshot != null && x.VehiclePlateSnapshot.Contains(search)) ||
                x.CompanyNameSnapshot.Contains(search));
        }

        if (filter.Status.HasValue)
            query = query.Where(x => x.Status == filter.Status.Value);
        if (filter.BusinessType.HasValue)
            query = query.Where(x => x.BusinessType == filter.BusinessType.Value);
        if (!string.IsNullOrWhiteSpace(filter.DriverId))
            query = query.Where(x => x.DriverId == filter.DriverId);
        if (filter.CompanyProfileId.HasValue)
            query = query.Where(x => x.CompanyProfileId == filter.CompanyProfileId.Value);
        if (filter.FromDate.HasValue)
            query = query.Where(x => (x.StartTime ?? x.CreatedAt) >= filter.FromDate.Value.Date);
        if (filter.ToDate.HasValue)
            query = query.Where(x => (x.StartTime ?? x.CreatedAt) < filter.ToDate.Value.Date.AddDays(1));

        return await query
            .OrderByDescending(x => x.CreatedAt)
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
                Status = x.Status,
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
        var detail = await db.Contracts.AsNoTracking()
            .Where(x => x.Id == id && !x.IsDeleted)
            .Select(x => new ContractDetailDto
            {
                Id = x.Id,
                ContractNumber = x.ContractNumber,
                BusinessType = x.BusinessType,
                ContractTypeId = x.ContractTypeId,
                Status = x.Status,
                IsSelfCreated = x.IsSelfCreated,
                IsLocked = x.Status == ContractStatus.Completed || x.Status == ContractStatus.Cancelled,
                CompanyProfileId = x.CompanyProfileId,
                CompanyName = x.CompanyNameSnapshot,
                CompanyRepresentativeName = x.CompanyRepresentativeSnapshot,
                DriverId = x.DriverId,
                DriverName = x.DriverNameSnapshot,
                DriverLicenseClass = x.DriverLicenseClassSnapshot,
                CustomerId = x.CustomerId,
                CustomerName = x.CustomerNameSnapshot,
                CustomerRepresentativeName = x.Customer != null && x.Customer.Type == CustomerType.Organization ? x.Customer.FullName : null,
                CustomerPhone = x.CustomerPhoneSnapshot,
                CustomerCitizenId = x.CustomerCitizenIdSnapshot,
                CustomerCitizenIdIssuedDate = x.Customer != null ? x.Customer.CitizenIdIssuedDate : null,
                CustomerTaxCode = x.Customer != null && x.Customer.Type == CustomerType.Organization ? x.Customer.TaxCode : null,
                CustomerIsCompany = x.Customer != null && x.Customer.Type == CustomerType.Organization,
                CustomerAddress = x.CustomerAddressSnapshot,
                CustomerTravelsWithGroup = x.CustomerTravelsWithGroup,
                AreaCode = x.AreaCode,
                VehicleId = x.VehicleId,
                VehiclePlate = x.VehiclePlateSnapshot,
                VehicleCode = x.Vehicle != null ? x.Vehicle.VehicleCode : null,
                VehicleBrand = x.VehicleBrandSnapshot,
                SeatCount = x.Vehicle != null ? x.Vehicle.SeatCount : null,
                ActualPassengerCount = x.ActualPassengerCount ?? x.Passengers.Count(p => !p.IsDeleted),
                OwnerName = x.VehicleOwnerNameSnapshot,
                OwnerCitizenId = x.VehicleOwnerCitizenIdSnapshot,
                OwnerCitizenIdIssuedDate = x.Driver != null ? x.Driver.CitizenIdIssuedDate : null,
                OperatingDriverName = x.OperatingDriverName,
                OperatingDriverPhoneNumber = x.OperatingDriverPhoneNumber,
                OperatingDriverLicenseNumber = x.OperatingDriverLicenseNumber,
                OperatingDriverLicenseClass = x.OperatingDriverLicenseClass,
                CargoName = x.CargoName,
                CargoWeight = x.CargoWeight,
                CargoUnit = x.CargoUnit,
                SecondDriverName = x.SecondDriverName,
                SecondDriverLicenseClass = x.SecondDriverLicenseClass,
                PickupLocation = x.PickupLocation,
                DropoffLocation = x.DropoffLocation,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                RouteDescription = x.RouteDescription,
                TotalKilometers = x.TotalKilometers,
                ContractValue = x.ContractValue,
                PaymentMethod = x.PaymentMethod,
                PaymentTime = x.PaymentTime,
                Note = x.Note,
                PdfFileUrl = x.PdfFileUrl,
                CreatedAt = x.CreatedAt,
                CreatedByUserId = x.CreatedBy,
                AssignedByUserId = x.AssignedByUserId,
                AssignedByName = x.AssignedByNameSnapshot,
                AssignedAt = x.AssignedAt,
                ReceivedAt = x.ReceivedAt,
                CompletedAt = x.CompletedAt,
                CancelledAt = x.CancelledAt,
                LockedAt = x.LockedAt,
                Passengers = x.Passengers
                    .Where(p => !p.IsDeleted)
                    .OrderBy(p => p.SortOrder)
                    .Select(p => new ContractPassengerDto
                    {
                        Id = p.Id,
                        SortOrder = p.SortOrder,
                        FullName = p.FullName,
                        BirthYear = p.BirthYear,
                        Note = p.Note
                    }).ToList(),
                Signatures = x.Signatures
                    .Where(s => !s.IsDeleted)
                    .OrderBy(s => s.ServerSignedAt)
                    .Select(s => new ContractSignatureDto
                    {
                        Id = s.Id,
                        Party = s.Party,
                        SignerName = s.SignerName,
                        SignatureFileUrl = s.SignatureFileUrl,
                        ServerSignedAt = s.ServerSignedAt
                    }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (detail is null)
            return null;

        var snapshotJson = await db.Contracts.AsNoTracking()
            .Where(x => x.Id == id && !x.IsDeleted)
            .Select(x => x.ContractDataJson)
            .FirstOrDefaultAsync(ct);
        var snapshot = ContractSnapshotData.FromJson(snapshotJson);
        if (snapshot is not null)
            ApplyImmutableSnapshot(detail, snapshot);

        // Chữ ký người lái thực tế là chữ ký riêng theo từng hợp đồng,
        // được Chủ xe ghi nhận trên điện thoại; không lấy từ chân ký Chủ xe.
        var driverSignature = detail.Signatures
            .Where(x => x.Party == SignatureParty.Driver)
            .OrderByDescending(x => x.ServerSignedAt)
            .FirstOrDefault();
        if (driverSignature is not null)
        {
            detail.DriverSignatureFileUrl = driverSignature.SignatureFileUrl;
            detail.DriverSignedAt = driverSignature.ServerSignedAt;
            if (!string.IsNullOrWhiteSpace(driverSignature.SignerName))
                detail.DriverName = driverSignature.SignerName;
        }

        detail.CreatedByName = await GetUserDisplayNameAsync(db, detail.CreatedByUserId, ct);
        if (string.IsNullOrWhiteSpace(detail.AssignedByName) && !string.IsNullOrWhiteSpace(detail.AssignedByUserId))
            detail.AssignedByName = await GetUserDisplayNameAsync(db, detail.AssignedByUserId, ct);

        // Hợp đồng phiên bản cũ chưa có các cột AssignedBy*.
        if (string.IsNullOrWhiteSpace(detail.AssignedByUserId) &&
            !string.Equals(detail.CreatedByUserId, detail.DriverId, StringComparison.Ordinal))
        {
            detail.AssignedByUserId = detail.CreatedByUserId;
            detail.AssignedByName = detail.CreatedByName;
            detail.AssignedAt = detail.CreatedAt;
        }

        return detail;
    }

    public async Task<SaveContractResult> CreateAsync(
        SaveContractRequest request,
        string currentUserId,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var access = await GetAccessAsync(db, currentUserId, ct);
        var canManage = access.IsOwner || access.IsAdmin;
        if (!canManage && !access.IsVehicleOwner)
            return new(false, null, "Tài khoản không có quyền tạo hợp đồng.");

        if (!Enum.IsDefined(request.BusinessType))
            return new(false, null, "Loại hợp đồng không hợp lệ.");
        if (access.IsVehicleOwner && request.BusinessType != ContractBusinessType.Passenger)
            return new(false, null, "Chủ xe chỉ được tự tạo Hợp đồng vận chuyển hành khách.");

        var passengerCount = CountPassengers(request.Passengers, request.CustomerTravelsWithGroup);
        if (request.BusinessType == ContractBusinessType.Passenger && passengerCount > 20)
            return new(false, null, "Danh sách hành khách tối đa 20 người theo mẫu PDF hiện tại.");

        var assignment = await ResolveAssignmentAsync(db, request, currentUserId, access, ct);
        if (assignment.Error is not null)
            return new(false, null, assignment.Error);

        var vehicle = assignment.Vehicle!;
        var vehicleOwner = assignment.VehicleOwner!;
        var company = assignment.CompanyProfile!;

        if (canManage)
        {
            var dispatchError = ValidateDispatchPrerequisites(company, vehicleOwner, request);
            if (dispatchError is not null)
                return new(false, null, dispatchError);
        }

        var type = await ResolveTypeAsync(db, request, ct);
        if (type is null)
            return new(false, null, "Chưa cấu hình loại hợp đồng đang hoạt động.");
        var template = await db.ContractTemplates
            .FirstOrDefaultAsync(x => x.ContractTypeId == type.Id && x.IsActive, ct);
        if (template is null)
            return new(false, null, "Chưa cấu hình mẫu hợp đồng đang hoạt động.");

        CustomerResolution customerResult;
        try
        {
            customerResult = await ResolveCustomerAsync(
                db,
                request,
                currentUserId,
                canManage,
                access.IsOwner,
                company.Id,
                existingCustomerId: null,
                ct);
        }
        catch (InvalidOperationException ex)
        {
            return new(false, null, ex.Message);
        }

        var now = DateTime.UtcNow;
        var actorName = await GetUserDisplayNameAsync(db, currentUserId, ct);
        var entity = new Contract
        {
            Id = Guid.NewGuid(),
            ContractNumber = string.IsNullOrWhiteSpace(request.ContractNumber)
                ? $"{DateTime.Now:yyyyMMddHHmmss}/{BusinessCode(request.BusinessType)}"
                : request.ContractNumber.Trim(),
            BusinessType = request.BusinessType,
            ContractTypeId = type.Id,
            ContractTemplateId = template.Id,
            CompanyProfileId = company.Id,
            DriverId = vehicleOwner.Id,
            CustomerId = customerResult.Customer.Id,
            VehicleId = vehicle.Id,
            Status = canManage ? ContractStatus.Assigned : ContractStatus.Created,
            IsSelfCreated = !canManage,
            AssignedByUserId = canManage ? currentUserId : null,
            AssignedByNameSnapshot = canManage ? actorName : null,
            AssignedAt = canManage ? now : null,
            ContractContentSnapshot = template.HtmlContent,
            CreatedBy = currentUserId,
            CreatedAt = now
        };

        Apply(entity, request);
        ApplySnapshots(entity, vehicleOwner, company, customerResult.Customer, vehicle);
        if (customerResult.IsProvisional)
            ApplyCustomerSnapshotsFromRequest(entity, request);
        var createdSnapshot = CaptureSnapshot(
            company,
            vehicleOwner,
            customerResult.Customer,
            vehicle,
            now,
            customerResult.IsProvisional ? request : null);
        ApplyOperatingDriverSnapshot(createdSnapshot, request);
        ApplyOperatingDriverEntitySnapshot(entity, request);
        entity.ContractDataJson = createdSnapshot.ToJson();
        AddPassengers(entity, request.Passengers, currentUserId);

        if (canManage)
        {
            entity.AuditLogs.Add(new ContractAuditLog
            {
                ContractId = entity.Id,
                Action = "AssignedToVehicleOwner",
                UserId = currentUserId,
                UserName = actorName,
                NewDataJson = $"{{\"vehicleOwnerId\":\"{vehicleOwner.Id}\",\"vehicleId\":\"{vehicle.Id}\"}}",
                CreatedAt = now
            });
            db.DriverNotifications.Add(new DriverNotification
            {
                DriverId = vehicleOwner.Id,
                Type = "ContractAssigned",
                Title = "Bạn được phát hợp đồng mới",
                Message = $"Hợp đồng {entity.ContractNumber} đã được {actorName} phát xuống cho xe {vehicle.PlateNumber}.",
                LinkUrl = $"/vehicle-owner/contracts/{entity.Id}",
                RelatedContractId = entity.Id,
                RelatedVehicleId = vehicle.Id,
                CreatedAt = now
            });
        }

        db.Contracts.Add(entity);
        await db.SaveChangesAsync(ct);
        return new(
            true,
            entity.Id,
            canManage
                ? customerResult.CreatedNew
                    ? "Đã tạo khách hàng mới, lưu khách hàng vào danh mục và phát hợp đồng cho tài khoản Chủ xe."
                    : "Đã tạo và phát hợp đồng cho tài khoản Chủ xe. Dữ liệu công ty, xe, chủ xe và khách hàng đã được chụp snapshot."
                : customerResult.CreatedNew
                    ? "Đã tạo hợp đồng. Hồ sơ khách hàng mới đang ở trạng thái tạm và chỉ được lưu chính thức khi hoàn thành hợp đồng."
                    : "Đã tạo hợp đồng và sử dụng lại hồ sơ khách hàng theo số điện thoại.");
    }

    public async Task<SaveContractResult> UpdateAsync(
        Guid id,
        SaveContractRequest request,
        string currentUserId,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.Contracts
            .Include(x => x.Passengers)
            .Include(x => x.Signatures)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (entity is null)
            return new(false, null, "Không tìm thấy hợp đồng.");
        if (IsFinal(entity.Status))
            return new(false, id, "Hợp đồng đã hủy hoặc đã hoàn thành nên bị khóa vĩnh viễn.");
        if (entity.Signatures.Any(x => !x.IsDeleted &&
            (x.Party == SignatureParty.Driver || x.Party == SignatureParty.Customer)))
            return new(false, id, "Hợp đồng đã có chữ ký người lái hoặc khách hàng nên nội dung không thể thay đổi.");

        var originalVehicleOwnerId = entity.DriverId;
        var existingSnapshot = ContractSnapshotData.FromJson(entity.ContractDataJson);

        var access = await GetAccessAsync(db, currentUserId, ct);
        var canManage = access.IsOwner || access.IsAdmin;
        if (access.IsAdmin && !access.IsOwner && !access.OfficeIds.Contains(entity.CompanyProfileId))
            return new(false, id, "Quản lý chỉ được cập nhật hợp đồng thuộc Công ty/Văn phòng được gán.");
        if (!canManage && (!access.IsVehicleOwner || !string.Equals(entity.DriverId, currentUserId, StringComparison.Ordinal)))
            return new(false, id, "Bạn không có quyền cập nhật hợp đồng này.");

        if (!canManage && request.BusinessType != ContractBusinessType.Passenger)
            return new(false, id, "Chủ xe chỉ được tự tạo Hợp đồng vận chuyển hành khách.");

        var passengerCount = CountPassengers(request.Passengers, request.CustomerTravelsWithGroup);
        if (request.BusinessType == ContractBusinessType.Passenger && passengerCount > 20)
            return new(false, id, "Danh sách hành khách tối đa 20 người theo mẫu PDF hiện tại.");

        if (!canManage)
            return await UpdateByVehicleOwnerAsync(db, entity, request, currentUserId, ct);

        var assignment = await ResolveAssignmentAsync(db, request, currentUserId, access, ct);
        if (assignment.Error is not null)
            return new(false, id, assignment.Error);

        var vehicle = assignment.Vehicle!;
        var vehicleOwner = assignment.VehicleOwner!;
        var company = assignment.CompanyProfile!;

        var dispatchError = ValidateDispatchPrerequisites(company, vehicleOwner, request);
        if (dispatchError is not null)
            return new(false, id, dispatchError);

        var type = await ResolveTypeAsync(db, request, ct);
        if (type is null)
            return new(false, id, "Chưa cấu hình loại hợp đồng đang hoạt động.");
        var template = await db.ContractTemplates
            .FirstOrDefaultAsync(x => x.ContractTypeId == type.Id && x.IsActive, ct);
        if (template is null)
            return new(false, id, "Chưa cấu hình mẫu hợp đồng đang hoạt động.");

        CustomerResolution customerResult;
        try
        {
            customerResult = await ResolveCustomerAsync(
                db,
                request,
                currentUserId,
                canManage: true,
                isOwner: access.IsOwner,
                company.Id,
                entity.CustomerId,
                ct);
        }
        catch (InvalidOperationException ex)
        {
            return new(false, id, ex.Message);
        }

        var now = DateTime.UtcNow;
        var actorName = await GetUserDisplayNameAsync(db, currentUserId, ct);
        entity.DriverId = vehicleOwner.Id;
        entity.ContractTypeId = type.Id;
        entity.ContractTemplateId = template.Id;
        entity.ContractContentSnapshot = template.HtmlContent;
        entity.CompanyProfileId = company.Id;
        entity.CustomerId = customerResult.Customer.Id;
        entity.VehicleId = vehicle.Id;
        entity.BusinessType = request.BusinessType;
        entity.IsSelfCreated = false;
        entity.Status = ContractStatus.Assigned;
        entity.AssignedByUserId = currentUserId;
        entity.AssignedByNameSnapshot = actorName;
        entity.AssignedAt = now;
        entity.ReceivedAt = null;
        Apply(entity, request);
        ApplySnapshots(entity, vehicleOwner, company, customerResult.Customer, vehicle);
        ApplyOperatingDriverEntitySnapshot(entity, request);
        var updatedSnapshot = ContractSnapshotData.Capture(
            company,
            vehicleOwner,
            customerResult.Customer,
            vehicle,
            now);
        ApplyOperatingDriverSnapshot(updatedSnapshot, request);

        // Chân ký Chủ xe là snapshot theo từng HĐ. Khi chỉ sửa nội dung hoặc
        // đổi xe nhưng vẫn cùng Chủ xe, giữ nguyên ảnh đã chụp lúc tạo HĐ.
        // Nếu đổi sang Chủ xe khác, dùng chân ký hiện tại của Chủ xe mới.
        if (string.Equals(originalVehicleOwnerId, vehicleOwner.Id, StringComparison.Ordinal))
            PreserveVehicleOwnerSignature(existingSnapshot, updatedSnapshot);

        entity.ContractDataJson = updatedSnapshot.ToJson();
        db.ContractPassengers.RemoveRange(entity.Passengers);
        AddPassengers(entity, request.Passengers, currentUserId);
        entity.UpdatedAt = now;
        entity.UpdatedBy = currentUserId;

        entity.AuditLogs.Add(new ContractAuditLog
        {
            ContractId = entity.Id,
            Action = "AssignedToVehicleOwner",
            UserId = currentUserId,
            UserName = actorName,
            NewDataJson = $"{{\"vehicleOwnerId\":\"{vehicleOwner.Id}\",\"vehicleId\":\"{vehicle.Id}\"}}",
            CreatedAt = now
        });
        db.DriverNotifications.Add(new DriverNotification
        {
            DriverId = vehicleOwner.Id,
            Type = "ContractAssigned",
            Title = "Hợp đồng được cập nhật/phát xuống",
            Message = $"Hợp đồng {entity.ContractNumber} đã được {actorName} phát cho xe {vehicle.PlateNumber}.",
            LinkUrl = $"/vehicle-owner/contracts/{entity.Id}",
            RelatedContractId = entity.Id,
            RelatedVehicleId = vehicle.Id,
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);
        return new(true, id, customerResult.CreatedNew
            ? "Đã tạo khách hàng mới, lưu vào danh mục và cập nhật/phát lại hợp đồng cho Chủ xe."
            : "Đã cập nhật và phát lại hợp đồng cho Chủ xe.");
    }

    public async Task<SaveContractResult> ReceiveAsync(
        Guid id,
        string currentUserId,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.Contracts
            .Include(x => x.Vehicle)
            .Include(x => x.Driver)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (entity is null)
            return new(false, null, "Không tìm thấy hợp đồng.");
        if (!string.Equals(entity.DriverId, currentUserId, StringComparison.Ordinal))
            return new(false, id, "Chỉ tài khoản Chủ xe được phát hợp đồng mới được nhận.");
        if (IsFinal(entity.Status))
            return new(false, id, "Hợp đồng đã bị khóa.");
        if (entity.IsSelfCreated)
            return new(false, id, "Hợp đồng do chính tài khoản tạo không cần thao tác nhận.");
        if (entity.Status != ContractStatus.Assigned)
            return new(false, id, "Hợp đồng không ở trạng thái chờ nhận.");
        if (entity.Vehicle is null || entity.Vehicle.AssignedDriverId != currentUserId)
            return new(false, id, "Xe của hợp đồng không còn được gán cho tài khoản này.");
        var receiveSnapshot = ContractSnapshotData.FromJson(entity.ContractDataJson);
        var ownerSignatureUrl = receiveSnapshot?.Vehicle.OwnerSignatureFileUrl
            ?? entity.Driver?.VehicleOwnerSignatureFileUrl;
        if (string.IsNullOrWhiteSpace(ownerSignatureUrl))
            return new(false, id, "Tài khoản Chủ xe chưa có chân ký. Vui lòng cập nhật chân ký trong hồ sơ tài khoản trước khi nhận hợp đồng.");

        var now = DateTime.UtcNow;
        entity.Status = ContractStatus.Received;
        entity.ReceivedAt = now;
        entity.UpdatedAt = now;
        entity.UpdatedBy = currentUserId;
        entity.AuditLogs.Add(new ContractAuditLog
        {
            ContractId = entity.Id,
            Action = "VehicleOwnerReceived",
            UserId = currentUserId,
            UserName = await GetUserDisplayNameAsync(db, currentUserId, ct),
            CreatedAt = now
        });
        await db.SaveChangesAsync(ct);
        return new(true, id, "Đã nhận hợp đồng. Bạn có thể cập nhật thông tin chuyến đi, người lái thực tế và danh sách hành khách.");
    }

    public async Task<SaveContractResult> CompleteAsync(
        Guid id,
        string currentUserId,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.Contracts
            .Include(x => x.CompanyProfile)
            .Include(x => x.Driver)
            .Include(x => x.Customer)
            .Include(x => x.Vehicle)
            .Include(x => x.Signatures)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (entity is null)
            return new(false, null, "Không tìm thấy hợp đồng.");
        if (!string.Equals(entity.DriverId, currentUserId, StringComparison.Ordinal))
            return new(false, id, "Chỉ Chủ xe của hợp đồng được hoàn thành hợp đồng.");
        if (IsFinal(entity.Status))
            return new(false, id, "Hợp đồng đã bị khóa.");
        if (!entity.IsSelfCreated && entity.Status != ContractStatus.Received)
            return new(false, id, "Hợp đồng được phát xuống phải ở trạng thái Đã nhận trước khi hoàn thành.");
        if (entity.IsSelfCreated && entity.Status is not ContractStatus.Created and not ContractStatus.Assigned and not ContractStatus.Received)
            return new(false, id, "Hợp đồng tự tạo không ở trạng thái cho phép hoàn thành.");
        if (entity.CompanyProfile is null || entity.Driver is null || entity.Customer is null || entity.Vehicle is null)
            return new(false, id, "Dữ liệu Công ty/Văn phòng, Chủ xe, khách hàng hoặc xe của hợp đồng không còn đầy đủ.");

        var existingSnapshot = ContractSnapshotData.FromJson(entity.ContractDataJson);

        if (entity.Vehicle.AssignedDriverId != currentUserId)
            return new(false, id, "Xe của hợp đồng không còn được gán cho tài khoản này.");
        if (string.IsNullOrWhiteSpace(entity.OperatingDriverName))
            return new(false, id, "Vui lòng nhập họ tên người trực tiếp điều khiển xe.");
        if (!AutomobileDrivingLicenseClasses.IsValid(entity.OperatingDriverLicenseClass))
            return new(false, id, "Vui lòng chọn hạng GPLX ô tô hợp lệ cho người trực tiếp điều khiển xe.");
        var officeSignatureUrl = existingSnapshot?.Company.RepresentativeSignatureFileUrl
            ?? entity.CompanyProfile.RepresentativeSignatureFileUrl;
        if (string.IsNullOrWhiteSpace(officeSignatureUrl))
            return new(false, id, "Hợp đồng chưa có snapshot chân ký Văn phòng đại diện.");
        var vehicleOwnerSignatureUrl = existingSnapshot?.Vehicle.OwnerSignatureFileUrl
            ?? entity.Driver.VehicleOwnerSignatureFileUrl;
        if (string.IsNullOrWhiteSpace(vehicleOwnerSignatureUrl))
            return new(false, id, "Tài khoản Chủ xe chưa có chân ký cố định.");
        if (!entity.Signatures.Any(x => !x.IsDeleted && x.Party == SignatureParty.Driver))
            return new(false, id, "Người lái thực tế chưa ký xác nhận hợp đồng trên điện thoại Chủ xe.");
        if (!entity.Signatures.Any(x => !x.IsDeleted && x.Party == SignatureParty.Customer))
            return new(false, id, "Khách hàng chưa ký xác nhận hợp đồng trên điện thoại Chủ xe.");

        var now = DateTime.UtcNow;
        var completedCustomer = await FinalizeSelfCreatedCustomerAsync(db, entity, currentUserId, now, ct);
        entity.Customer = completedCustomer;
        entity.CustomerId = completedCustomer.Id;
        ApplySnapshots(entity, entity.Driver, entity.CompanyProfile, completedCustomer, entity.Vehicle);
        entity.DriverNameSnapshot = entity.OperatingDriverName.Trim();
        entity.DriverLicenseNumberSnapshot = N(entity.OperatingDriverLicenseNumber);
        entity.DriverLicenseClassSnapshot = N(entity.OperatingDriverLicenseClass);

        var completedSnapshot = ContractSnapshotData.Capture(
            entity.CompanyProfile,
            entity.Driver,
            completedCustomer,
            entity.Vehicle,
            now);

        // Giữ nguyên các ảnh chữ ký đã chụp tại thời điểm lập HĐ. Việc tài khoản
        // hoặc Công ty/Văn phòng ký lại chỉ áp dụng cho HĐ tạo sau đó.
        if (existingSnapshot is not null)
        {
            completedSnapshot.Company.RepresentativeSignatureFileUrl = existingSnapshot.Company.RepresentativeSignatureFileUrl;
            completedSnapshot.Company.RepresentativeSignatureHash = existingSnapshot.Company.RepresentativeSignatureHash;
            completedSnapshot.Company.RepresentativeSignedAt = existingSnapshot.Company.RepresentativeSignedAt;
            PreserveVehicleOwnerSignature(existingSnapshot, completedSnapshot);
        }

        completedSnapshot.Driver.UserId = null;
        completedSnapshot.Driver.FullName = entity.OperatingDriverName.Trim();
        completedSnapshot.Driver.PhoneNumber = N(entity.OperatingDriverPhoneNumber);
        completedSnapshot.Driver.CitizenId = null;
        completedSnapshot.Driver.CitizenIdIssuedDate = null;
        completedSnapshot.Driver.CitizenIdIssuedPlace = null;
        completedSnapshot.Driver.Address = null;
        completedSnapshot.Driver.AreaCode = null;
        completedSnapshot.Driver.DriverLicenseNumber = N(entity.OperatingDriverLicenseNumber);
        completedSnapshot.Driver.DriverLicenseClass = N(entity.OperatingDriverLicenseClass);
        completedSnapshot.Driver.DriverLicenseIssuedDate = null;
        completedSnapshot.Driver.DriverLicenseExpiryDate = null;
        // Chữ ký người lái được lưu ở ContractSignatures theo từng HĐ.
        // Không ghi chân ký Chủ xe vào snapshot Driver.
        completedSnapshot.Driver.SignatureFileUrl = null;
        completedSnapshot.Driver.SignatureHash = null;
        completedSnapshot.Driver.SignedAt = null;
        entity.ContractDataJson = completedSnapshot.ToJson();
        entity.Status = ContractStatus.Completed;
        entity.CompletedAt = now;
        entity.LockedAt = now;
        entity.UpdatedAt = now;
        entity.UpdatedBy = currentUserId;
        entity.ContractHash = BuildContractHash(entity);
        entity.AuditLogs.Add(new ContractAuditLog
        {
            ContractId = entity.Id,
            Action = "CompletedAndLocked",
            UserId = currentUserId,
            UserName = await GetUserDisplayNameAsync(db, currentUserId, ct),
            CreatedAt = now
        });
        await db.SaveChangesAsync(ct);
        return new(true, id, "Đã hoàn thành và khóa cố định hợp đồng. Hợp đồng không thể chỉnh sửa hoặc hủy.");
    }

    public async Task<SaveContractResult> CancelByDriverAsync(
        Guid id,
        string currentUserId,
        string? reason = null,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.Contracts
            .Include(x => x.Signatures)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (entity is null)
            return new(false, null, "Không tìm thấy hợp đồng.");
        if (IsFinal(entity.Status))
            return new(false, id, "Hợp đồng đã hủy hoặc đã hoàn thành.");

        var access = await GetAccessAsync(db, currentUserId, ct);
        var canManage = access.IsOwner || access.IsAdmin;
        if (access.IsAdmin && !access.IsOwner && !access.OfficeIds.Contains(entity.CompanyProfileId))
            return new(false, id, "Quản lý chỉ được hủy hợp đồng thuộc Công ty/Văn phòng được gán.");
        if (!canManage && !string.Equals(entity.DriverId, currentUserId, StringComparison.Ordinal))
            return new(false, id, "Bạn không có quyền hủy hợp đồng này.");
        if (entity.Signatures.Any(x => !x.IsDeleted &&
            (x.Party == SignatureParty.Driver || x.Party == SignatureParty.Customer)))
            return new(false, id, "Hợp đồng đã có chữ ký người lái hoặc khách hàng nên không thể hủy.");

        var now = DateTime.UtcNow;
        entity.Status = ContractStatus.Cancelled;
        entity.CancelledAt = now;
        entity.LockedAt = now;
        entity.CancelReason = string.IsNullOrWhiteSpace(reason)
            ? "Hủy hợp đồng và tạo hợp đồng mới khi cần thay đổi."
            : reason.Trim();
        entity.UpdatedAt = now;
        entity.UpdatedBy = currentUserId;
        if (entity.IsSelfCreated && IsProvisionalCustomer(entity.Customer))
            SoftDeleteProvisionalCustomer(entity.Customer, currentUserId);

        entity.AuditLogs.Add(new ContractAuditLog
        {
            ContractId = entity.Id,
            Action = "CancelledAndLocked",
            UserId = currentUserId,
            UserName = await GetUserDisplayNameAsync(db, currentUserId, ct),
            NewDataJson = $"{{\"reason\":\"{EscapeJson(entity.CancelReason)}\"}}",
            CreatedAt = now
        });
        await db.SaveChangesAsync(ct);
        return new(true, id, "Đã hủy và khóa hợp đồng. Muốn thay đổi thông tin, vui lòng tạo hợp đồng mới.");
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        string currentUserId,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.Contracts
            .Include(x => x.Customer)
            .Include(x => x.Signatures)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (entity is null || IsFinal(entity.Status))
            return false;
        if (entity.Signatures.Any(x => !x.IsDeleted &&
            (x.Party == SignatureParty.Driver || x.Party == SignatureParty.Customer)))
            return false;

        var access = await GetAccessAsync(db, currentUserId, ct);
        var canManage = access.IsOwner || access.IsAdmin;
        if (access.IsAdmin && !access.IsOwner && !access.OfficeIds.Contains(entity.CompanyProfileId))
            return false;
        if (!canManage && !string.Equals(entity.DriverId, currentUserId, StringComparison.Ordinal))
            return false;

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedBy = currentUserId;
        if (entity.IsSelfCreated && IsProvisionalCustomer(entity.Customer))
            SoftDeleteProvisionalCustomer(entity.Customer, currentUserId);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<SaveContractResult> UpdateByVehicleOwnerAsync(
        ApplicationDbContext db,
        Contract entity,
        SaveContractRequest request,
        string currentUserId,
        CancellationToken ct)
    {
        if (entity.Status is not ContractStatus.Created and not ContractStatus.Assigned and not ContractStatus.Received)
            return new(false, entity.Id, "Hợp đồng không còn ở trạng thái cho phép Chủ xe cập nhật.");

        var existingSnapshot = ContractSnapshotData.FromJson(entity.ContractDataJson);

        // Hợp đồng được Owner/Admin phát xuống giữ nguyên xe và khách hàng.
        if (!entity.IsSelfCreated)
        {
            if (request.VehicleId.HasValue && request.VehicleId != entity.VehicleId)
                return new(false, entity.Id, "Không được đổi xe của hợp đồng đã được phát xuống.");
            if (DriverChangedCustomer(entity, request))
                return new(false, entity.Id, "Không được thay đổi khách hàng của hợp đồng do Chủ hệ thống/Quản lý phát xuống.");
        }

        Vehicle? vehicle;
        ApplicationUser? vehicleOwner;
        CompanyProfile? company;
        Customer customer;
        var customerIsProvisional = false;

        if (entity.IsSelfCreated)
        {
            var access = new UserAccess(false, false, true, []);
            var assignment = await ResolveAssignmentAsync(db, request, currentUserId, access, ct);
            if (assignment.Error is not null)
                return new(false, entity.Id, assignment.Error);
            if (assignment.Vehicle is null || assignment.VehicleOwner is null || assignment.CompanyProfile is null)
                return new(false, entity.Id, "Không thể xác định đầy đủ xe, chủ xe và Công ty/Văn phòng.");

            vehicle = assignment.Vehicle;
            vehicleOwner = assignment.VehicleOwner;
            company = assignment.CompanyProfile;

            CustomerResolution customerResult;
            try
            {
                customerResult = await ResolveCustomerAsync(
                    db,
                    request,
                    currentUserId,
                    canManage: false,
                    isOwner: false,
                    company.Id,
                    entity.CustomerId,
                    ct);
            }
            catch (InvalidOperationException ex)
            {
                return new(false, entity.Id, ex.Message);
            }
            customer = customerResult.Customer;
            customerIsProvisional = customerResult.IsProvisional;
            entity.VehicleId = vehicle.Id;
            entity.CompanyProfileId = company.Id;
            entity.DriverId = currentUserId;
            entity.CustomerId = customer.Id;
        }
        else
        {
            vehicle = await db.Vehicles
                .Include(x => x.OfficeVehicles)
                .FirstOrDefaultAsync(x => x.Id == entity.VehicleId && x.IsActive && !x.IsDeleted, ct);
            vehicleOwner = await db.Users.FirstOrDefaultAsync(x => x.Id == currentUserId, ct);
            customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == entity.CustomerId && !x.IsDeleted, ct)
                ?? throw new InvalidOperationException("Không tìm thấy khách hàng của hợp đồng.");
            company = await db.CompanyProfiles.FirstOrDefaultAsync(x => x.Id == entity.CompanyProfileId && x.IsActive && !x.IsDeleted, ct);
            var vehicleStillAssignedToOffice = vehicle?.OfficeVehicles.Any(x => x.IsActive && !x.IsDeleted &&
                x.AssignedTo == null && x.CompanyProfileId == entity.CompanyProfileId) == true;
            if (vehicle is null || company is null || vehicleOwner is null || vehicle.AssignedDriverId != currentUserId || !vehicleStillAssignedToOffice)
                return new(false, entity.Id, "Xe của hợp đồng không còn hợp lệ, không còn thuộc văn phòng hợp đồng hoặc không còn được gán cho tài khoản.");
        }

        var passengerCount = CountPassengers(request.Passengers, request.CustomerTravelsWithGroup);
        if (passengerCount > 20)
            return new(false, entity.Id, "Danh sách hành khách tối đa 20 người theo mẫu PDF hiện tại.");
        if (!string.IsNullOrWhiteSpace(request.OperatingDriverLicenseClass) &&
            !AutomobileDrivingLicenseClasses.IsValid(request.OperatingDriverLicenseClass))
            return new(false, entity.Id, "Hạng GPLX ô tô của người trực tiếp điều khiển xe không hợp lệ.");

        Apply(entity, request);
        ApplySnapshots(entity, vehicleOwner!, company!, customer, vehicle!);
        ApplyOperatingDriverEntitySnapshot(entity, request);
        if (customerIsProvisional)
            ApplyCustomerSnapshotsFromRequest(entity, request);
        var updatedSnapshot = CaptureSnapshot(
            company!,
            vehicleOwner!,
            customer,
            vehicle!,
            DateTime.UtcNow,
            customerIsProvisional ? request : null);
        ApplyOperatingDriverSnapshot(updatedSnapshot, request);
        PreserveVehicleOwnerSignature(existingSnapshot, updatedSnapshot);
        entity.ContractDataJson = updatedSnapshot.ToJson();
        db.ContractPassengers.RemoveRange(entity.Passengers);
        AddPassengers(entity, request.Passengers, currentUserId);

        // Assigned thể hiện nội dung đã lưu và có thể ghi nhận chữ ký khách hàng.
        // Received vẫn được giữ nếu tài khoản đã bấm nhận trước đó.
        entity.Status = entity.Status == ContractStatus.Received
            ? ContractStatus.Received
            : entity.IsSelfCreated
                ? ContractStatus.Created
                : ContractStatus.Assigned;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = currentUserId;
        entity.AuditLogs.Add(new ContractAuditLog
        {
            ContractId = entity.Id,
            Action = "VehicleOwnerUpdatedContract",
            UserId = currentUserId,
            UserName = await GetUserDisplayNameAsync(db, currentUserId, ct),
            NewDataJson = $"{{\"passengerCount\":{passengerCount},\"vehicleId\":\"{vehicle!.Id}\"}}",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return new(true, entity.Id, "Đã lưu thông tin tài xế chạy, chuyến đi và danh sách hành khách.");
    }

    private sealed record UserAccess(
        bool IsOwner,
        bool IsAdmin,
        bool IsVehicleOwner,
        HashSet<Guid> OfficeIds);

    private sealed record CustomerResolution(Customer Customer, bool CreatedNew, bool IsProvisional);

    private async Task<UserAccess> GetAccessAsync(
        ApplicationDbContext db,
        string userId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return new(false, false, false, []);

        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId && x.IsActive && !x.IsDeleted, ct);
        if (user is null)
            return new(false, false, false, []);

        var isOwner = await userManager.IsInRoleAsync(user, "Owner");
        var isAdmin = await userManager.IsInRoleAsync(user, "Admin");
        var isVehicleOwner = await userManager.IsInRoleAsync(user, "VehicleOwner");

        var officeIds = isAdmin && !isOwner
            ? (await db.AdminOffices.AsNoTracking()
                .Where(x => x.AdminUserId == userId && x.IsActive && !x.IsDeleted &&
                            x.CompanyProfile.IsActive && !x.CompanyProfile.IsDeleted)
                .Select(x => x.CompanyProfileId)
                .Distinct()
                .ToListAsync(ct)).ToHashSet()
            : new HashSet<Guid>();

        if (isAdmin && !isOwner && officeIds.Count == 0)
            isAdmin = false;

        if (isVehicleOwner && !string.Equals(user.RegistrationStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            isVehicleOwner = false;

        return new(isOwner, isAdmin, isVehicleOwner, officeIds);
    }

    private static async Task<string> GetUserDisplayNameAsync(
        ApplicationDbContext db,
        string? userId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return "Hệ thống";
        return await db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(ct) ?? userId;
    }

    private static Task<ContractType?> ResolveTypeAsync(
        ApplicationDbContext db,
        SaveContractRequest request,
        CancellationToken ct)
    {
        var code = request.BusinessType switch
        {
            ContractBusinessType.Passenger => "PASSENGER",
            ContractBusinessType.Cargo => "CARGO",
            _ => string.Empty
        };
        return db.ContractTypes.FirstOrDefaultAsync(x => x.Code == code && x.IsActive, ct);
    }

    private static async Task<CustomerResolution> ResolveCustomerAsync(
        ApplicationDbContext db,
        SaveContractRequest request,
        string currentUserId,
        bool canManage,
        bool isOwner,
        Guid companyProfileId,
        Guid? existingCustomerId,
        CancellationToken ct)
    {
        if (canManage)
        {
            // Owner/Quản lý có thể chọn khách hàng đã có hoặc tạo mới trực tiếp ngay trên form Hợp đồng.
            // Khách hàng mới được ghi chính thức vào Customers trong cùng DbContext/SaveChanges với Hợp đồng,
            // do đó không còn bắt buộc phải qua màn hình Quản lý khách hàng trước.
            if (!request.CustomerId.HasValue)
            {
                if (MissingCustomerInfo(request))
                    throw new InvalidOperationException("Vui lòng nhập đầy đủ họ tên/tên công ty và số điện thoại khách hàng mới.");

                if (request.CustomerIsCompany)
                {
                    if (string.IsNullOrWhiteSpace(request.CustomerRepresentativeName))
                        throw new InvalidOperationException("Khách hàng là công ty thì bắt buộc nhập tên người đại diện.");
                    if (string.IsNullOrWhiteSpace(request.CustomerTaxCode))
                        throw new InvalidOperationException("Khách hàng là công ty thì bắt buộc nhập mã số thuế.");
                }

                var now = DateTime.UtcNow;
                var customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    Type = request.CustomerIsCompany ? CustomerType.Organization : CustomerType.Individual,
                    FullName = request.CustomerIsCompany ? request.CustomerRepresentativeName!.Trim() : request.CustomerName.Trim(),
                    OrganizationName = request.CustomerIsCompany ? request.CustomerName.Trim() : null,
                    TaxCode = request.CustomerIsCompany ? N(request.CustomerTaxCode) : null,
                    PhoneNumber = request.CustomerPhone.Trim(),
                    CitizenId = N(request.CustomerCitizenId),
                    CitizenIdIssuedDate = request.CustomerCitizenIdIssuedDate,
                    Address = N(request.CustomerAddress),
                    CreatedByDriverId = currentUserId,
                    LastUsedAt = now,
                    CreatedBy = currentUserId,
                    CreatedAt = now,
                    UpdatedBy = currentUserId,
                    UpdatedAt = now
                };

                db.Customers.Add(customer);
                return new(customer, true, false);
            }

            var id = request.CustomerId.Value;
            var query = db.Customers
                .Include(x => x.CreatedByDriver)
                .Where(x => !x.IsDeleted)
                .AsQueryable();
            if (!isOwner)
            {
                query = query.Where(x =>
                    x.CreatedByDriverId == currentUserId ||
                    x.Contracts.Any(c => c.CompanyProfileId == companyProfileId) ||
                    db.Vehicles.Any(v => !v.IsDeleted && v.AssignedDriverId == x.CreatedByDriverId &&
                        v.OfficeVehicles.Any(ov => ov.IsActive && !ov.IsDeleted && ov.AssignedTo == null &&
                                                   ov.CompanyProfileId == companyProfileId)));
            }

            var selected = await query.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
            if (selected is null)
                throw new InvalidOperationException("Không tìm thấy khách hàng hoặc khách hàng không thuộc phạm vi quản lý.");
            selected.LastUsedAt = DateTime.UtcNow;
            return new(selected, false, false);
        }

        if (MissingCustomerInfo(request))
            throw new InvalidOperationException("Vui lòng nhập đầy đủ họ tên/tên công ty, người đại diện (nếu là công ty) và số điện thoại khách hàng.");
        if (request.CustomerIsCompany && string.IsNullOrWhiteSpace(request.CustomerTaxCode))
            throw new InvalidOperationException("Khách hàng là công ty thì bắt buộc nhập mã số thuế.");

        // Chủ xe khi TẠO hợp đồng mới luôn phải nhập khách hàng mới trên form.
        // Không cho phép truyền CustomerId để chọn/đọc lại khách hàng cũ, kể cả qua request thủ công.
        if (!existingCustomerId.HasValue && request.CustomerId.HasValue)
            throw new InvalidOperationException("Chủ xe không được chọn khách hàng đã có khi tạo hợp đồng mới.");

        // Chỉ cho phép dùng CustomerId khi đang cập nhật chính hợp đồng hiện tại.
        var requestedCustomerId = existingCustomerId.HasValue
            ? request.CustomerId ?? existingCustomerId
            : null;
        Customer? selectedCustomer = null;
        if (requestedCustomerId.HasValue)
        {
            selectedCustomer = await db.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(x =>
                x.Id == requestedCustomerId.Value && !x.IsDeleted && x.CreatedByDriverId == currentUserId,
                ct);
            if (selectedCustomer is null)
                throw new InvalidOperationException("Chủ xe chỉ được chọn khách hàng do chính tài khoản đã tạo.");

            if (!IsProvisionalCustomer(selectedCustomer))
            {
                selectedCustomer.LastUsedAt = DateTime.UtcNow;
                return new(selectedCustomer, false, false);
            }
        }

        if (selectedCustomer is not null)
        {
            UpdateProvisionalCustomer(selectedCustomer, request, currentUserId);
            return new(selectedCustomer, false, true);
        }

        var provisionalCustomer = new Customer
        {
            Id = Guid.NewGuid(),
            Type = request.CustomerIsCompany ? CustomerType.Organization : CustomerType.Individual,
            FullName = request.CustomerIsCompany ? request.CustomerRepresentativeName?.Trim() ?? string.Empty : request.CustomerName.Trim(),
            OrganizationName = request.CustomerIsCompany ? request.CustomerName.Trim() : null,
            TaxCode = request.CustomerIsCompany ? N(request.CustomerTaxCode) : null,
            PhoneNumber = BuildProvisionalPhone(),
            CitizenId = N(request.CustomerCitizenId),
            CitizenIdIssuedDate = request.CustomerCitizenIdIssuedDate,
            Address = N(request.CustomerAddress),
            CreatedByDriverId = currentUserId,
            CreatedBy = currentUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = currentUserId
        };
        db.Customers.Add(provisionalCustomer);
        return new(provisionalCustomer, true, true);
    }

    private static string BuildProvisionalPhone()
        => $"PEND{Guid.NewGuid():N}"[..20];

    private static bool IsProvisionalCustomer(Customer? customer)
        => customer is not null && customer.PhoneNumber.StartsWith("PEND", StringComparison.OrdinalIgnoreCase);

    private static void UpdateProvisionalCustomer(
        Customer customer,
        SaveContractRequest request,
        string currentUserId)
    {
        customer.Type = request.CustomerIsCompany ? CustomerType.Organization : CustomerType.Individual;
        customer.FullName = request.CustomerIsCompany ? request.CustomerRepresentativeName?.Trim() ?? string.Empty : request.CustomerName.Trim();
        customer.OrganizationName = request.CustomerIsCompany ? request.CustomerName.Trim() : null;
        customer.TaxCode = request.CustomerIsCompany ? N(request.CustomerTaxCode) : null;
        customer.CitizenId = N(request.CustomerCitizenId);
        customer.CitizenIdIssuedDate = request.CustomerCitizenIdIssuedDate;
        customer.Address = N(request.CustomerAddress);
        customer.UpdatedAt = DateTime.UtcNow;
        customer.UpdatedBy = currentUserId;
    }

    private static void SoftDeleteProvisionalCustomer(Customer customer, string currentUserId)
    {
        if (!IsProvisionalCustomer(customer) || customer.IsDeleted)
            return;

        customer.IsDeleted = true;
        customer.DeletedAt = DateTime.UtcNow;
        customer.DeletedBy = currentUserId;
        customer.UpdatedAt = DateTime.UtcNow;
        customer.UpdatedBy = currentUserId;
    }

    private static void ApplyCustomerSnapshotsFromRequest(Contract entity, SaveContractRequest request)
    {
        entity.CustomerNameSnapshot = request.CustomerName.Trim();
        entity.CustomerPhoneSnapshot = request.CustomerPhone.Trim();
        entity.CustomerCitizenIdSnapshot = N(request.CustomerCitizenId);
        entity.CustomerAddressSnapshot = N(request.CustomerAddress);
    }

    private static ContractSnapshotData CaptureSnapshot(
        CompanyProfile company,
        ApplicationUser vehicleOwner,
        Customer customer,
        Vehicle vehicle,
        DateTime capturedAt,
        SaveContractRequest? provisionalCustomerRequest)
    {
        var snapshot = ContractSnapshotData.Capture(company, vehicleOwner, customer, vehicle, capturedAt);
        if (provisionalCustomerRequest is null)
            return snapshot;

        snapshot.Customer.OrganizationName = provisionalCustomerRequest.CustomerIsCompany ? provisionalCustomerRequest.CustomerName.Trim() : null;
        snapshot.Customer.FullName = provisionalCustomerRequest.CustomerIsCompany
            ? N(provisionalCustomerRequest.CustomerRepresentativeName)
            : provisionalCustomerRequest.CustomerName.Trim();
        snapshot.Customer.TaxCode = provisionalCustomerRequest.CustomerIsCompany ? N(provisionalCustomerRequest.CustomerTaxCode) : null;
        snapshot.Customer.PhoneNumber = provisionalCustomerRequest.CustomerPhone.Trim();
        snapshot.Customer.CitizenId = N(provisionalCustomerRequest.CustomerCitizenId);
        snapshot.Customer.CitizenIdIssuedDate = provisionalCustomerRequest.CustomerCitizenIdIssuedDate;
        snapshot.Customer.Address = N(provisionalCustomerRequest.CustomerAddress);
        return snapshot;
    }

    private static async Task<Customer> FinalizeSelfCreatedCustomerAsync(
        ApplicationDbContext db,
        Contract contract,
        string currentUserId,
        DateTime now,
        CancellationToken ct)
    {
        var current = contract.Customer;
        if (!contract.IsSelfCreated || !IsProvisionalCustomer(current))
        {
            current.LastUsedAt = now;
            return current;
        }

        var phone = contract.CustomerPhoneSnapshot.Trim();
        if (string.IsNullOrWhiteSpace(phone) || phone.StartsWith("PEND", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Số điện thoại khách hàng của hợp đồng không hợp lệ.");

        var finalCustomer = await db.Customers.FirstOrDefaultAsync(x =>
            x.CreatedByDriverId == currentUserId && x.PhoneNumber == phone &&
            !x.PhoneNumber.StartsWith("PEND"),
            ct);

        var snapshot = ContractSnapshotData.FromJson(contract.ContractDataJson);
        var isCompany = !string.IsNullOrWhiteSpace(snapshot?.Customer.OrganizationName);
        var companyName = isCompany ? snapshot?.Customer.OrganizationName?.Trim() : null;
        var representativeName = isCompany ? snapshot?.Customer.FullName?.Trim() : null;

        if (finalCustomer is null)
        {
            finalCustomer = new Customer
            {
                Id = Guid.NewGuid(),
                Type = isCompany ? CustomerType.Organization : CustomerType.Individual,
                FullName = isCompany ? representativeName ?? contract.CustomerNameSnapshot : contract.CustomerNameSnapshot,
                OrganizationName = companyName,
                TaxCode = isCompany ? N(snapshot?.Customer.TaxCode) : null,
                PhoneNumber = phone,
                CitizenId = N(contract.CustomerCitizenIdSnapshot),
                CitizenIdIssuedDate = snapshot?.Customer.CitizenIdIssuedDate,
                Address = N(contract.CustomerAddressSnapshot),
                CreatedByDriverId = currentUserId,
                CreatedBy = currentUserId,
                CreatedAt = now,
                LastUsedAt = now
            };
            db.Customers.Add(finalCustomer);
        }
        else
        {
            // Có thể trùng số điện thoại với khách đã từng giao dịch, nhưng hợp đồng hiện tại
            // phải giữ đúng dữ liệu Chủ xe vừa nhập; không lấy ngược thông tin cũ lên hợp đồng.
            finalCustomer.Type = isCompany ? CustomerType.Organization : CustomerType.Individual;
            finalCustomer.FullName = isCompany
                ? representativeName ?? contract.CustomerNameSnapshot
                : contract.CustomerNameSnapshot;
            finalCustomer.OrganizationName = companyName;
            finalCustomer.TaxCode = isCompany ? N(snapshot?.Customer.TaxCode) : null;
            finalCustomer.CitizenId = N(contract.CustomerCitizenIdSnapshot);
            finalCustomer.CitizenIdIssuedDate = snapshot?.Customer.CitizenIdIssuedDate;
            finalCustomer.Address = N(contract.CustomerAddressSnapshot);
            finalCustomer.LastUsedAt = now;
            finalCustomer.UpdatedBy = currentUserId;
            finalCustomer.UpdatedAt = now;
        }

        SoftDeleteProvisionalCustomer(current, currentUserId);
        return finalCustomer;
    }

    private async Task<(Vehicle? Vehicle, ApplicationUser? VehicleOwner, CompanyProfile? CompanyProfile, string? Error)> ResolveAssignmentAsync(
        ApplicationDbContext db,
        SaveContractRequest request,
        string currentUserId,
        UserAccess access,
        CancellationToken ct)
    {
        var canManage = access.IsOwner || access.IsAdmin;
        Vehicle? vehicle = null;
        if (request.VehicleId.HasValue)
        {
            vehicle = await db.Vehicles
                .Include(x => x.AssignedDriver)
                .Include(x => x.OfficeVehicles)
                    .ThenInclude(x => x.CompanyProfile)
                .FirstOrDefaultAsync(x => x.Id == request.VehicleId.Value && x.IsActive && !x.IsDeleted, ct);
        }
        else if (!string.IsNullOrWhiteSpace(request.VehiclePlate))
        {
            var plate = request.VehiclePlate.Trim().ToUpperInvariant();
            vehicle = await db.Vehicles
                .Include(x => x.AssignedDriver)
                .Include(x => x.OfficeVehicles)
                    .ThenInclude(x => x.CompanyProfile)
                .FirstOrDefaultAsync(x => x.PlateNumber == plate && x.IsActive && !x.IsDeleted, ct);
        }

        if (vehicle is null)
            return (null, null, null, "Vui lòng chọn xe đang hoạt động.");

        var activeOfficeLinks = vehicle.OfficeVehicles
            .Where(x => x.IsActive && !x.IsDeleted && x.AssignedTo == null &&
                        x.CompanyProfile.IsActive && !x.CompanyProfile.IsDeleted)
            .ToList();
        if (activeOfficeLinks.Count == 0)
            return (vehicle, null, null, "Xe chưa được gán Công ty/Văn phòng đang hoạt động.");

        var allowedOfficeLinks = access.IsOwner
            ? activeOfficeLinks
            : access.IsAdmin
                ? activeOfficeLinks.Where(x => access.OfficeIds.Contains(x.CompanyProfileId)).ToList()
                : activeOfficeLinks;
        if (allowedOfficeLinks.Count == 0)
            return (vehicle, null, null, "Xe không thuộc Công ty/Văn phòng trong phạm vi quản lý.");

        var requestedOfficeId = request.CompanyProfileId;
        var selectedOfficeLink = requestedOfficeId.HasValue
            ? allowedOfficeLinks.FirstOrDefault(x => x.CompanyProfileId == requestedOfficeId.Value)
            : allowedOfficeLinks.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.AssignedFrom).FirstOrDefault();
        if (selectedOfficeLink is null)
            return (vehicle, null, null, "Công ty/Văn phòng đã chọn không được gán cho xe hoặc nằm ngoài phạm vi quản lý.");

        string vehicleOwnerId;
        if (canManage)
        {
            if (string.IsNullOrWhiteSpace(vehicle.AssignedDriverId))
                return (vehicle, null, selectedOfficeLink.CompanyProfile, "Xe chưa được gán tài khoản Chủ xe. Hãy gán tại mục Xe trước khi phát hợp đồng.");
            vehicleOwnerId = vehicle.AssignedDriverId;
            if (!string.IsNullOrWhiteSpace(request.DriverId) &&
                !string.Equals(request.DriverId, vehicleOwnerId, StringComparison.Ordinal))
            {
                var assignedName = vehicle.AssignedDriver?.FullName ?? vehicleOwnerId;
                return (vehicle, null, selectedOfficeLink.CompanyProfile, $"Xe {vehicle.PlateNumber} đang được gán cho {assignedName}. Không thể phát cho tài khoản khác.");
            }
        }
        else
        {
            vehicleOwnerId = currentUserId;
            if (!string.Equals(vehicle.AssignedDriverId, currentUserId, StringComparison.Ordinal))
            {
                var assignedName = vehicle.AssignedDriver?.FullName;
                return (vehicle, null, selectedOfficeLink.CompanyProfile, string.IsNullOrWhiteSpace(assignedName)
                    ? "Bạn chỉ được chọn xe đang được gán cho tài khoản của mình."
                    : $"Xe {vehicle.PlateNumber} đang được gán cho {assignedName}.");
            }
        }

        var vehicleOwner = vehicle.AssignedDriver;
        if (vehicleOwner is null || !vehicleOwner.IsActive || vehicleOwner.IsDeleted ||
            !string.Equals(vehicleOwner.RegistrationStatus, "Approved", StringComparison.OrdinalIgnoreCase) ||
            !await userManager.IsInRoleAsync(vehicleOwner, "VehicleOwner"))
            return (vehicle, null, selectedOfficeLink.CompanyProfile, "Tài khoản được gán xe không hoạt động, chưa được duyệt hoặc không có vai trò Chủ xe.");

        if (!canManage && string.IsNullOrWhiteSpace(vehicleOwner.VehicleOwnerSignatureFileUrl))
            return (vehicle, null, selectedOfficeLink.CompanyProfile,
                $"Tài khoản Chủ xe {vehicleOwner.FullName} chưa có chân ký. Hãy cập nhật chân ký tài khoản trước khi tự tạo hợp đồng.");

        return (vehicle, vehicleOwner, selectedOfficeLink.CompanyProfile, null);
    }

    private static string? ValidateDispatchPrerequisites(
        CompanyProfile company,
        ApplicationUser vehicleOwner,
        SaveContractRequest request)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(company.RepresentativeSignatureFileUrl))
            missing.Add("chân ký Văn phòng đại diện");
        if (string.IsNullOrWhiteSpace(vehicleOwner.VehicleOwnerSignatureFileUrl))
            missing.Add("chân ký Chủ xe");

        if (missing.Count > 0)
            return $"Chưa thể phát hợp đồng xuống tài khoản nhận HĐ. Cần đủ 2 chân ký đầu tiên: {string.Join(" và ", missing)}.";

        if (string.IsNullOrWhiteSpace(request.OperatingDriverName))
            return "Vui lòng nhập họ và tên người trực tiếp điều khiển xe trước khi phát hợp đồng.";

        if (!AutomobileDrivingLicenseClasses.IsValid(request.OperatingDriverLicenseClass))
            return "Vui lòng chọn hạng GPLX ô tô hợp lệ cho người trực tiếp điều khiển xe trước khi phát hợp đồng.";

        return null;
    }

    private static void ApplyOperatingDriverEntitySnapshot(Contract entity, SaveContractRequest request)
    {
        entity.DriverNameSnapshot = N(request.OperatingDriverName) ?? string.Empty;
        entity.DriverLicenseNumberSnapshot = N(request.OperatingDriverLicenseNumber);
        entity.DriverLicenseClassSnapshot = N(request.OperatingDriverLicenseClass);
    }

    private static void ApplyOperatingDriverSnapshot(ContractSnapshotData snapshot, SaveContractRequest request)
    {
        snapshot.Driver.UserId = null;
        snapshot.Driver.FullName = N(request.OperatingDriverName);
        snapshot.Driver.PhoneNumber = N(request.OperatingDriverPhoneNumber);
        snapshot.Driver.CitizenId = null;
        snapshot.Driver.CitizenIdIssuedDate = null;
        snapshot.Driver.CitizenIdIssuedPlace = null;
        snapshot.Driver.Address = null;
        snapshot.Driver.AreaCode = null;
        snapshot.Driver.DriverLicenseNumber = N(request.OperatingDriverLicenseNumber);
        snapshot.Driver.DriverLicenseClass = N(request.OperatingDriverLicenseClass);
        snapshot.Driver.DriverLicenseIssuedDate = null;
        snapshot.Driver.DriverLicenseExpiryDate = null;

        // Người lái thực tế không nhất thiết là Chủ xe/tài khoản nhận HĐ.
        // Không được dùng chân ký Chủ xe để giả lập chữ ký người lái.
        snapshot.Driver.SignatureFileUrl = null;
        snapshot.Driver.SignatureHash = null;
        snapshot.Driver.SignedAt = null;
    }

    private static void PreserveVehicleOwnerSignature(
        ContractSnapshotData? source,
        ContractSnapshotData target)
    {
        if (source is null || string.IsNullOrWhiteSpace(source.Vehicle.OwnerSignatureFileUrl))
            return;

        target.Vehicle.OwnerSignatureFileUrl = source.Vehicle.OwnerSignatureFileUrl;
        target.Vehicle.OwnerSignatureHash = source.Vehicle.OwnerSignatureHash;
        target.Vehicle.OwnerSignedAt = source.Vehicle.OwnerSignedAt;
    }

    private static void ApplyImmutableSnapshot(ContractDetailDto detail, ContractSnapshotData snapshot)
    {
        detail.CompanyName = snapshot.Company.DisplayName;
        detail.CompanyRepresentativeName = snapshot.Company.RepresentativeName;
        detail.CompanyRepresentativeSignatureFileUrl = snapshot.Company.RepresentativeSignatureFileUrl;
        detail.CompanyRepresentativeSignedAt = snapshot.Company.RepresentativeSignedAt;
        detail.DriverName = snapshot.Driver.FullName ?? string.Empty;
        detail.DriverLicenseClass = snapshot.Driver.DriverLicenseClass;
        detail.DriverSignatureFileUrl = snapshot.Driver.SignatureFileUrl;
        detail.DriverSignedAt = snapshot.Driver.SignedAt;
        detail.CustomerIsCompany = !string.IsNullOrWhiteSpace(snapshot.Customer.OrganizationName);
        detail.CustomerName = detail.CustomerIsCompany
            ? snapshot.Customer.OrganizationName ?? string.Empty
            : snapshot.Customer.FullName ?? string.Empty;
        detail.CustomerRepresentativeName = detail.CustomerIsCompany ? snapshot.Customer.FullName : null;
        detail.CustomerPhone = snapshot.Customer.PhoneNumber ?? string.Empty;
        detail.CustomerCitizenId = snapshot.Customer.CitizenId;
        detail.CustomerCitizenIdIssuedDate = snapshot.Customer.CitizenIdIssuedDate;
        detail.CustomerTaxCode = snapshot.Customer.TaxCode;
        detail.CustomerAddress = snapshot.Customer.Address;
        detail.VehiclePlate = snapshot.Vehicle.PlateNumber;
        detail.VehicleCode = snapshot.Vehicle.VehicleCode;
        detail.VehicleBrand = string.IsNullOrWhiteSpace(snapshot.Vehicle.BrandModel)
            ? null
            : snapshot.Vehicle.BrandModel;
        detail.SeatCount = snapshot.Vehicle.SeatCount;
        detail.OwnerName = snapshot.Vehicle.OwnerName;
        detail.OwnerCitizenId = snapshot.Vehicle.OwnerCitizenId;
        detail.OwnerCitizenIdIssuedDate = snapshot.Vehicle.OwnerCitizenIdIssuedDate;
        detail.VehicleOwnerSignatureFileUrl = snapshot.Vehicle.OwnerSignatureFileUrl;
        detail.VehicleOwnerSignedAt = snapshot.Vehicle.OwnerSignedAt;
    }

    private static void Apply(Contract entity, SaveContractRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ContractNumber))
            entity.ContractNumber = request.ContractNumber.Trim();
        entity.AreaCode = string.IsNullOrWhiteSpace(request.AreaCode) ? "N/A" : request.AreaCode.Trim();
        entity.CustomerTravelsWithGroup = request.CustomerTravelsWithGroup;
        entity.CargoName = N(request.CargoName);
        entity.CargoWeight = request.CargoWeight;
        entity.CargoUnit = N(request.CargoUnit);
        entity.ActualPassengerCount = CountPassengers(request.Passengers, request.CustomerTravelsWithGroup);
        entity.OperatingDriverName = N(request.OperatingDriverName);
        entity.OperatingDriverPhoneNumber = N(request.OperatingDriverPhoneNumber);
        entity.OperatingDriverLicenseNumber = N(request.OperatingDriverLicenseNumber);
        entity.OperatingDriverLicenseClass = N(request.OperatingDriverLicenseClass);
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

    private static void ApplySnapshots(
        Contract entity,
        ApplicationUser vehicleOwner,
        CompanyProfile company,
        Customer customer,
        Vehicle vehicle)
    {
        entity.CompanyNameSnapshot = company.CompanyName;
        entity.CompanyTaxCodeSnapshot = company.TaxCode;
        entity.CompanyAddressSnapshot = company.Address;
        entity.CompanyRepresentativeSnapshot = company.RepresentativeName;
        entity.CompanyRepresentativePositionSnapshot = company.RepresentativePosition;
        entity.DriverNameSnapshot = vehicleOwner.FullName;
        entity.DriverLicenseNumberSnapshot = vehicleOwner.DriverLicenseNumber;
        entity.DriverLicenseClassSnapshot = vehicleOwner.DriverLicenseClass;
        entity.CustomerNameSnapshot = customer.FullName;
        entity.CustomerPhoneSnapshot = customer.PhoneNumber;
        entity.CustomerCitizenIdSnapshot = customer.CitizenId;
        entity.CustomerAddressSnapshot = customer.Address;
        entity.VehiclePlateSnapshot = vehicle.PlateNumber;
        entity.VehicleBrandSnapshot = string.Join(" ", new[] { vehicle.Brand, vehicle.Model }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        entity.VehicleOwnerNameSnapshot = vehicleOwner.FullName;
        entity.VehicleOwnerCitizenIdSnapshot = vehicleOwner.CitizenId;
    }

    private static void AddPassengers(
        Contract entity,
        IEnumerable<ContractPassengerDto> passengers,
        string userId)
    {
        foreach (var item in passengers
                     .Where(x => !string.IsNullOrWhiteSpace(x.FullName))
                     .Select((x, index) => (x, index)))
        {
            entity.Passengers.Add(new ContractPassenger
            {
                SortOrder = item.index + 1,
                FullName = item.x.FullName.Trim(),
                BirthYear = item.x.BirthYear,
                Note = N(item.x.Note),
                CreatedBy = userId
            });
        }
    }

    private static bool MissingCustomerInfo(SaveContractRequest request)
        => string.IsNullOrWhiteSpace(request.CustomerName) ||
           string.IsNullOrWhiteSpace(request.CustomerPhone) ||
           (request.CustomerIsCompany && string.IsNullOrWhiteSpace(request.CustomerRepresentativeName));

    private static bool DriverChangedCustomer(Contract contract, SaveContractRequest request)
        => (request.CustomerId.HasValue && request.CustomerId != contract.CustomerId) ||
           (!string.IsNullOrWhiteSpace(request.CustomerName) && !Same(request.CustomerName, contract.CustomerNameSnapshot)) ||
           (!string.IsNullOrWhiteSpace(request.CustomerPhone) && !Same(request.CustomerPhone, contract.CustomerPhoneSnapshot)) ||
           (!string.IsNullOrWhiteSpace(request.CustomerCitizenId) && !Same(request.CustomerCitizenId, contract.CustomerCitizenIdSnapshot)) ||
           (!string.IsNullOrWhiteSpace(request.CustomerAddress) && !Same(request.CustomerAddress, contract.CustomerAddressSnapshot));

    private static bool IsFinal(ContractStatus status)
        => status is ContractStatus.Completed or ContractStatus.Cancelled or ContractStatus.Expired or ContractStatus.Invalidated;

    private static bool Same(string? left, string? right)
        => string.Equals(N(left), N(right), StringComparison.Ordinal);

    private static string BusinessCode(ContractBusinessType type)
        => type == ContractBusinessType.Cargo ? "HH" : "HK";

    private static int CountPassengers(
        IEnumerable<ContractPassengerDto> passengers,
        bool customerTravelsWithGroup)
        => passengers.Count(x => !string.IsNullOrWhiteSpace(x.FullName)) +
           (customerTravelsWithGroup ? 1 : 0);

    private static string BuildContractHash(Contract contract)
    {
        var payload = string.Join("|",
            contract.Id,
            contract.ContractNumber,
            contract.BusinessType,
            contract.CompanyProfileId,
            contract.DriverId,
            contract.CustomerId,
            contract.VehicleId,
            contract.ContractDataJson,
            contract.Status,
            contract.CompletedAt?.ToString("O"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static string EscapeJson(string? value)
        => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string? N(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
