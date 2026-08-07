using HTX586CONTRACT.Application.Admins.AdminAccounts;
using HTX586CONTRACT.Application.Common;

namespace HTX586CONTRACT.Application.Abstractions;

public interface IAdminAccountService
{
    Task<IReadOnlyList<AdminAccountListItem>> GetAccountsAsync(string? keyword = null, string? role = null, CancellationToken cancellationToken = default);
    Task<CreateAdminAccountResult> CreateAccountAsync(CreateAdminAccountRequest request, CancellationToken cancellationToken = default);
    Task<AdminAccountDetail?> GetDetailAsync(string userId, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateAccountAsync(UpdateAdminAccountRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> ResetPasswordToDefaultAsync(string userId, string? actorUserId = null, CancellationToken cancellationToken = default);
    Task<ServiceResult> SetActiveAsync(string userId, bool isActive, string? actorUserId = null, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteAsync(string userId, string? deletedByUserId = null, CancellationToken cancellationToken = default);
}
