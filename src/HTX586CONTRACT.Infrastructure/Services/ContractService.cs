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
    UserManager<ApplicationUser> userManager) : IContractService
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
                CustomerPhone = x.CustomerPhoneSnapshot,
                CustomerCitizenId = x.CustomerCitizenIdSnapshot,
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
                OwnerCitizenIdIssuedDate = x.Vehicle != null ? x.Vehicle.OwnerCitizenIdIssuedDate : null,
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
            return new(false, null, "VehicleOwner chỉ được tự tạo Hợp đồng vận chuyển hành khách.");

        var passengerCount = CountPassengers(request.Passengers, request.CustomerTravelsWithGroup);
        if (request.BusinessType == ContractBusinessType.Passenger && passengerCount > 20)
            return new(false, null, "Danh sách hành khách tối đa 20 người theo mẫu PDF hiện tại.");

        var assignment = await ResolveAssignmentAsync(db, request, currentUserId, access, ct);
        if (assignment.Error is not null)
            return new(false, null, assignment.Error);

        var vehicle = assignment.Vehicle!;
        var vehicleOwner = assignment.VehicleOwner!;
        var company = vehicle.CompanyProfile!;

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
        entity.ContractDataJson = CaptureSnapshot(
            company,
            vehicleOwner,
            customerResult.Customer,
            vehicle,
            now,
            customerResult.IsProvisional ? request : null).ToJson();
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
                ? "Đã tạo và phát hợp đồng cho tài khoản VehicleOwner. Dữ liệu công ty, xe, chủ xe và khách hàng đã được chụp snapshot."
                : customerResult.CreatedNew
                    ? "Đã tạo hợp đồng. Hồ sơ khách hàng mới đang ở trạng thái tạm và chỉ được lưu chính thức khi hoàn thành hợp đồng."
                    : "Đã tạo hợp đồng và sử dụng lại hồ sơ khách hàng cá nhân theo số điện thoại.");
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
        if (entity.Signatures.Any(x => !x.IsDeleted && x.Party == SignatureParty.Customer))
            return new(false, id, "Khách hàng đã ký nên nội dung hợp đồng không thể thay đổi.");

        var access = await GetAccessAsync(db, currentUserId, ct);
        var canManage = access.IsOwner || access.IsAdmin;
        if (access.IsAdmin && !access.IsOwner && entity.CompanyProfileId != access.CompanyProfileId)
            return new(false, id, "Admin chỉ được cập nhật hợp đồng thuộc Công ty/Văn phòng được gán.");
        if (!canManage && (!access.IsVehicleOwner || !string.Equals(entity.DriverId, currentUserId, StringComparison.Ordinal)))
            return new(false, id, "Bạn không có quyền cập nhật hợp đồng này.");

        if (!canManage && request.BusinessType != ContractBusinessType.Passenger)
            return new(false, id, "VehicleOwner chỉ được tự tạo Hợp đồng vận chuyển hành khách.");

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
        var company = vehicle.CompanyProfile!;
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
        entity.ContractDataJson = ContractSnapshotData.Capture(
            company,
            vehicleOwner,
            customerResult.Customer,
            vehicle,
            now).ToJson();
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
        return new(true, id, "Đã cập nhật và phát lại hợp đồng cho VehicleOwner.");
    }

    public async Task<SaveContractResult> ReceiveAsync(
        Guid id,
        string currentUserId,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.Contracts
            .Include(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (entity is null)
            return new(false, null, "Không tìm thấy hợp đồng.");
        if (!string.Equals(entity.DriverId, currentUserId, StringComparison.Ordinal))
            return new(false, id, "Chỉ tài khoản VehicleOwner được phát hợp đồng mới được nhận.");
        if (IsFinal(entity.Status))
            return new(false, id, "Hợp đồng đã bị khóa.");
        if (entity.IsSelfCreated)
            return new(false, id, "Hợp đồng do chính tài khoản tạo không cần thao tác nhận.");
        if (entity.Status != ContractStatus.Assigned)
            return new(false, id, "Hợp đồng không ở trạng thái chờ nhận.");
        if (entity.Vehicle is null || entity.Vehicle.AssignedDriverId != currentUserId)
            return new(false, id, "Xe của hợp đồng không còn được gán cho tài khoản này.");
        if (string.IsNullOrWhiteSpace(entity.Vehicle.AccountDriverSignatureFileUrl))
            return new(false, id, $"Bạn phải tạo chữ ký tài xế một lần cho xe {entity.Vehicle.PlateNumber} trước khi nhận hợp đồng.");

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
            return new(false, id, "Chỉ VehicleOwner của hợp đồng được hoàn thành hợp đồng.");
        if (IsFinal(entity.Status))
            return new(false, id, "Hợp đồng đã bị khóa.");
        if (!entity.IsSelfCreated && entity.Status != ContractStatus.Received)
            return new(false, id, "Hợp đồng được phát xuống phải ở trạng thái Đã nhận trước khi hoàn thành.");
        if (entity.IsSelfCreated && entity.Status is not ContractStatus.Created and not ContractStatus.Assigned and not ContractStatus.Received)
            return new(false, id, "Hợp đồng tự tạo không ở trạng thái cho phép hoàn thành.");
        if (entity.CompanyProfile is null || entity.Driver is null || entity.Customer is null || entity.Vehicle is null)
            return new(false, id, "Dữ liệu Công ty/Văn phòng, VehicleOwner, khách hàng hoặc xe của hợp đồng không còn đầy đủ.");
        if (entity.Vehicle.AssignedDriverId != currentUserId)
            return new(false, id, "Xe của hợp đồng không còn được gán cho tài khoản này.");
        if (string.IsNullOrWhiteSpace(entity.OperatingDriverName))
            return new(false, id, "Vui lòng nhập họ tên tài xế trực tiếp điều khiển xe.");
        if (string.IsNullOrWhiteSpace(entity.CompanyProfile.RepresentativeSignatureFileUrl))
            return new(false, id, "Công ty/Văn phòng chưa có chữ ký đại diện cố định.");
        if (string.IsNullOrWhiteSpace(entity.Vehicle.OwnerSignatureFileUrl))
            return new(false, id, "Xe chưa có chữ ký chủ sở hữu cố định.");
        if (string.IsNullOrWhiteSpace(entity.Vehicle.AccountDriverSignatureFileUrl))
            return new(false, id, "Xe chưa có chữ ký tài xế của tài khoản VehicleOwner.");
        if (!entity.Signatures.Any(x => !x.IsDeleted && x.Party == SignatureParty.Customer))
            return new(false, id, "Khách hàng chưa ký xác nhận hợp đồng.");

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
        completedSnapshot.Driver.FullName = entity.OperatingDriverName.Trim();
        completedSnapshot.Driver.PhoneNumber = N(entity.OperatingDriverPhoneNumber);
        completedSnapshot.Driver.DriverLicenseNumber = N(entity.OperatingDriverLicenseNumber);
        completedSnapshot.Driver.DriverLicenseClass = N(entity.OperatingDriverLicenseClass);
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
        if (access.IsAdmin && !access.IsOwner && entity.CompanyProfileId != access.CompanyProfileId)
            return new(false, id, "Admin chỉ được hủy hợp đồng thuộc Công ty/Văn phòng được gán.");
        if (!canManage && !string.Equals(entity.DriverId, currentUserId, StringComparison.Ordinal))
            return new(false, id, "Bạn không có quyền hủy hợp đồng này.");

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
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (entity is null || IsFinal(entity.Status))
            return false;

        var access = await GetAccessAsync(db, currentUserId, ct);
        var canManage = access.IsOwner || access.IsAdmin;
        if (access.IsAdmin && !access.IsOwner && entity.CompanyProfileId != access.CompanyProfileId)
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
            return new(false, entity.Id, "Hợp đồng không còn ở trạng thái cho phép VehicleOwner cập nhật.");

        // Hợp đồng được Owner/Admin phát xuống giữ nguyên xe và khách hàng.
        if (!entity.IsSelfCreated)
        {
            if (request.VehicleId.HasValue && request.VehicleId != entity.VehicleId)
                return new(false, entity.Id, "Không được đổi xe của hợp đồng đã được phát xuống.");
            if (DriverChangedCustomer(entity, request))
                return new(false, entity.Id, "Không được thay đổi khách hàng của hợp đồng do Owner/Admin phát xuống.");
        }

        Vehicle? vehicle;
        ApplicationUser? vehicleOwner;
        CompanyProfile? company;
        Customer customer;
        var customerIsProvisional = false;

        if (entity.IsSelfCreated)
        {
            var access = new UserAccess(false, false, true, null);
            var assignment = await ResolveAssignmentAsync(db, request, currentUserId, access, ct);
            if (assignment.Error is not null)
                return new(false, entity.Id, assignment.Error);
            vehicle = assignment.Vehicle;
            vehicleOwner = assignment.VehicleOwner;
            company = vehicle!.CompanyProfile;

            CustomerResolution customerResult;
            try
            {
                customerResult = await ResolveCustomerAsync(
                    db,
                    request,
                    currentUserId,
                    canManage: false,
                    isOwner: false,
                    company!.Id,
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
                .Include(x => x.CompanyProfile)
                .FirstOrDefaultAsync(x => x.Id == entity.VehicleId && x.IsActive && !x.IsDeleted, ct);
            vehicleOwner = await db.Users.FirstOrDefaultAsync(x => x.Id == currentUserId, ct);
            customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == entity.CustomerId && !x.IsDeleted, ct)
                ?? throw new InvalidOperationException("Không tìm thấy khách hàng của hợp đồng.");
            company = vehicle?.CompanyProfile;
            if (vehicle is null || company is null || vehicleOwner is null || vehicle.AssignedDriverId != currentUserId)
                return new(false, entity.Id, "Xe của hợp đồng không còn hợp lệ hoặc không còn được gán cho tài khoản.");
        }

        var passengerCount = CountPassengers(request.Passengers, request.CustomerTravelsWithGroup);
        if (passengerCount > 20)
            return new(false, entity.Id, "Danh sách hành khách tối đa 20 người theo mẫu PDF hiện tại.");

        Apply(entity, request);
        ApplySnapshots(entity, vehicleOwner!, company!, customer, vehicle!);
        if (customerIsProvisional)
            ApplyCustomerSnapshotsFromRequest(entity, request);
        entity.ContractDataJson = CaptureSnapshot(
            company!,
            vehicleOwner!,
            customer,
            vehicle!,
            DateTime.UtcNow,
            customerIsProvisional ? request : null).ToJson();
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
        Guid? CompanyProfileId);

    private sealed record CustomerResolution(Customer Customer, bool CreatedNew, bool IsProvisional);

    private async Task<UserAccess> GetAccessAsync(
        ApplicationDbContext db,
        string userId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return new(false, false, false, null);

        var user = await db.Users.AsNoTracking()
            .Include(x => x.CompanyProfile)
            .FirstOrDefaultAsync(x => x.Id == userId && x.IsActive && !x.IsDeleted, ct);
        if (user is null)
            return new(false, false, false, null);

        var isOwner = await userManager.IsInRoleAsync(user, "Owner");
        var isAdmin = await userManager.IsInRoleAsync(user, "Admin");
        var isVehicleOwner = await userManager.IsInRoleAsync(user, "VehicleOwner");

        if (isAdmin && !isOwner &&
            (!user.CompanyProfileId.HasValue || user.CompanyProfile is null ||
             !user.CompanyProfile.IsActive || user.CompanyProfile.IsDeleted))
            return new(false, false, false, null);

        if (isVehicleOwner && !string.Equals(user.RegistrationStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            isVehicleOwner = false;

        return new(isOwner, isAdmin, isVehicleOwner, user.CompanyProfileId);
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
            if (!request.CustomerId.HasValue)
                throw new InvalidOperationException("Owner/Admin phải chọn khách hàng có sẵn. Hãy tạo khách hàng tại Quản lý Khách hàng trước.");

            var id = request.CustomerId.Value;
            var query = db.Customers
                .Include(x => x.CreatedByDriver)
                .Where(x => !x.IsDeleted)
                .AsQueryable();
            if (!isOwner)
            {
                query = query.Where(x =>
                    x.CreatedByDriver.CompanyProfileId == companyProfileId ||
                    x.Contracts.Any(c => c.CompanyProfileId == companyProfileId) ||
                    db.Vehicles.Any(v => !v.IsDeleted && v.CompanyProfileId == companyProfileId &&
                                             v.AssignedDriverId == x.CreatedByDriverId));
            }

            var selected = await query.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
            if (selected is null)
                throw new InvalidOperationException("Không tìm thấy khách hàng hoặc khách hàng không thuộc phạm vi Công ty/Văn phòng.");
            selected.LastUsedAt = DateTime.UtcNow;
            return new(selected, false, false);
        }

        if (MissingCustomerInfo(request))
            throw new InvalidOperationException("Vui lòng nhập đầy đủ họ tên và số điện thoại khách hàng.");

        var phone = request.CustomerPhone.Trim();
        var requestedCustomerId = request.CustomerId ?? existingCustomerId;
        Customer? selectedCustomer = null;
        if (requestedCustomerId.HasValue)
        {
            selectedCustomer = await db.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(x =>
                x.Id == requestedCustomerId.Value && !x.IsDeleted && x.CreatedByDriverId == currentUserId,
                ct);
            if (selectedCustomer is null)
                throw new InvalidOperationException("VehicleOwner chỉ được chọn khách hàng do chính tài khoản đã tạo.");

            if (!IsProvisionalCustomer(selectedCustomer))
            {
                selectedCustomer.LastUsedAt = DateTime.UtcNow;
                return new(selectedCustomer, false, false);
            }
        }

        var existingCustomer = await db.Customers.FirstOrDefaultAsync(x =>
            x.CreatedByDriverId == currentUserId && x.PhoneNumber == phone &&
            !x.PhoneNumber.StartsWith("PEND"),
            ct);
        if (existingCustomer is not null)
        {
            existingCustomer.LastUsedAt = DateTime.UtcNow;
            if (selectedCustomer is not null)
                SoftDeleteProvisionalCustomer(selectedCustomer, currentUserId);
            return new(existingCustomer, false, false);
        }

        if (selectedCustomer is not null)
        {
            UpdateProvisionalCustomer(selectedCustomer, request, currentUserId);
            return new(selectedCustomer, false, true);
        }

        var provisionalCustomer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = request.CustomerName.Trim(),
            PhoneNumber = BuildProvisionalPhone(),
            CitizenId = N(request.CustomerCitizenId),
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
        customer.FullName = request.CustomerName.Trim();
        customer.CitizenId = N(request.CustomerCitizenId);
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

        snapshot.Customer.FullName = provisionalCustomerRequest.CustomerName.Trim();
        snapshot.Customer.PhoneNumber = provisionalCustomerRequest.CustomerPhone.Trim();
        snapshot.Customer.CitizenId = N(provisionalCustomerRequest.CustomerCitizenId);
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

        if (finalCustomer is null)
        {
            finalCustomer = new Customer
            {
                Id = Guid.NewGuid(),
                FullName = contract.CustomerNameSnapshot,
                PhoneNumber = phone,
                CitizenId = N(contract.CustomerCitizenIdSnapshot),
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
            finalCustomer.LastUsedAt = now;
        }

        SoftDeleteProvisionalCustomer(current, currentUserId);
        return finalCustomer;
    }

    private async Task<(Vehicle? Vehicle, ApplicationUser? VehicleOwner, string? Error)> ResolveAssignmentAsync(
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
                .Include(x => x.CompanyProfile)
                .Include(x => x.AssignedDriver)
                .FirstOrDefaultAsync(x => x.Id == request.VehicleId.Value && x.IsActive && !x.IsDeleted, ct);
        }
        else if (!string.IsNullOrWhiteSpace(request.VehiclePlate))
        {
            var plate = request.VehiclePlate.Trim().ToUpperInvariant();
            vehicle = await db.Vehicles
                .Include(x => x.CompanyProfile)
                .Include(x => x.AssignedDriver)
                .FirstOrDefaultAsync(x => x.PlateNumber == plate && x.IsActive && !x.IsDeleted, ct);
        }

        if (vehicle is null)
            return (null, null, "Vui lòng chọn xe đang hoạt động.");
        if (vehicle.CompanyProfile is null || !vehicle.CompanyProfile.IsActive || vehicle.CompanyProfile.IsDeleted)
            return (vehicle, null, "Xe chưa thuộc Công ty/Văn phòng đang hoạt động.");
        if (access.IsAdmin && !access.IsOwner && vehicle.CompanyProfileId != access.CompanyProfileId)
            return (vehicle, null, "Admin chỉ được chọn xe thuộc Công ty/Văn phòng được gán.");

        string vehicleOwnerId;
        if (canManage)
        {
            if (string.IsNullOrWhiteSpace(vehicle.AssignedDriverId))
                return (vehicle, null, "Xe chưa được gán tài khoản VehicleOwner. Hãy gán tại Quản lý Xe và chủ sở hữu trước khi phát hợp đồng.");
            vehicleOwnerId = vehicle.AssignedDriverId;
            if (!string.IsNullOrWhiteSpace(request.DriverId) &&
                !string.Equals(request.DriverId, vehicleOwnerId, StringComparison.Ordinal))
            {
                var assignedName = vehicle.AssignedDriver?.FullName ?? vehicleOwnerId;
                return (vehicle, null, $"Xe {vehicle.PlateNumber} đang được gán cho {assignedName}. Không thể phát cho tài khoản khác.");
            }
        }
        else
        {
            vehicleOwnerId = currentUserId;
            if (!string.Equals(vehicle.AssignedDriverId, currentUserId, StringComparison.Ordinal))
            {
                var assignedName = vehicle.AssignedDriver?.FullName;
                return (vehicle, null, string.IsNullOrWhiteSpace(assignedName)
                    ? "Bạn chỉ được chọn xe đang được gán cho tài khoản của mình."
                    : $"Xe {vehicle.PlateNumber} đang được gán cho {assignedName}.");
            }
        }

        var vehicleOwner = vehicle.AssignedDriver;
        if (vehicleOwner is null || !vehicleOwner.IsActive || vehicleOwner.IsDeleted ||
            !string.Equals(vehicleOwner.RegistrationStatus, "Approved", StringComparison.OrdinalIgnoreCase) ||
            !await userManager.IsInRoleAsync(vehicleOwner, "VehicleOwner"))
            return (vehicle, null, "Tài khoản được gán xe không hoạt động, chưa được duyệt hoặc không có role VehicleOwner.");

        return (vehicle, vehicleOwner, null);
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
        detail.CustomerName = snapshot.Customer.FullName ?? string.Empty;
        detail.CustomerPhone = snapshot.Customer.PhoneNumber ?? string.Empty;
        detail.CustomerCitizenId = snapshot.Customer.CitizenId;
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
        entity.VehicleOwnerNameSnapshot = vehicle.OwnerName;
        entity.VehicleOwnerCitizenIdSnapshot = vehicle.OwnerCitizenId;
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
           string.IsNullOrWhiteSpace(request.CustomerPhone);

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
