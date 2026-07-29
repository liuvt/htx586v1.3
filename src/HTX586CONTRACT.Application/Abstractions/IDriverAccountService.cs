using HTX586CONTRACT.Application.Admins.DriverAccounts;

namespace HTX586CONTRACT.Application.Abstractions;

public interface IDriverAccountService
{
    Task<string> SubmitRegistrationAsync(SelfRegisterDriverRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DriverRegistrationRequestDto>> GetPendingRegistrationsAsync(string adminId, CancellationToken cancellationToken = default);
    Task<int> GetUnseenPendingRegistrationCountAsync(string adminId, CancellationToken cancellationToken = default);
    Task<DriverRegistrationRequestDto?> GetRegistrationDetailAsync(string userId, string adminId, CancellationToken cancellationToken = default);
    Task MarkRegistrationViewedAsync(string userId, string viewerUserId, CancellationToken cancellationToken = default);
    Task ReviewRegistrationAsync(string userId, bool approve, string? note, string reviewerUserId, CancellationToken cancellationToken = default);

    Task UpdateAsync(
        string userId,
        UpdateDriverAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<DriverAccountDetailDto?> GetDetailAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriverAccountDto>> GetListAsync(
        DriverAccountFilter filter,
        CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(
        string userId,
        string newPassword,
        string adminId,
        CancellationToken cancellationToken = default);
}
