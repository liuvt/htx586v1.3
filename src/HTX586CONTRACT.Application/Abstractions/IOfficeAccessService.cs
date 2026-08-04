namespace HTX586CONTRACT.Application.Abstractions;

public interface IOfficeAccessService
{
    Task<HashSet<Guid>> GetManagedOfficeIdsAsync(string userId, bool isOwner, CancellationToken cancellationToken = default);
    Task<bool> CanManageOfficeAsync(string userId, Guid officeId, bool isOwner, CancellationToken cancellationToken = default);
    Task<bool> CanManageVehicleAsync(string userId, Guid vehicleId, bool isOwner, CancellationToken cancellationToken = default);
}
