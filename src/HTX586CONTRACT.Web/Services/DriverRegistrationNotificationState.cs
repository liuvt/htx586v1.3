using HTX586CONTRACT.Application.Abstractions;

namespace HTX586CONTRACT.Web.Services;

public sealed class DriverRegistrationNotificationState(IDriverAccountService driverAccountService)
{
    public int UnseenCount { get; private set; }
    public event Action? Changed;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        UnseenCount = await driverAccountService.GetUnseenPendingRegistrationCountAsync(cancellationToken);
        Changed?.Invoke();
    }
}
