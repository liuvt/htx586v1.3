using HTX586CONTRACT.Application.Abstractions;

namespace HTX586CONTRACT.Web.Services;

public sealed class DriverRegistrationNotificationState(IDriverAccountService driverAccountService)
{
    public int UnseenCount { get; private set; }
    public event Action? Changed;

    public async Task RefreshAsync(string adminId, CancellationToken cancellationToken = default)
    {
        UnseenCount = string.IsNullOrWhiteSpace(adminId)
            ? 0
            : await driverAccountService.GetUnseenPendingRegistrationCountAsync(adminId, cancellationToken);
        Changed?.Invoke();
    }
}
