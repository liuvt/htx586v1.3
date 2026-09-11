using HTX586CONTRACT.Application.Drivers;

namespace HTX586CONTRACT.Application.Abstractions;

public interface ISavedDriverService
{
    Task<IReadOnlyList<SavedDriverDto>> GetMineAsync(string currentUserId, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task<SaveSavedDriverResult> SaveAsync(SaveSavedDriverRequest request, string currentUserId, CancellationToken cancellationToken = default);
    Task<SaveSavedDriverResult> DeleteAsync(string driverLicenseNumber, string currentUserId, CancellationToken cancellationToken = default);
}
