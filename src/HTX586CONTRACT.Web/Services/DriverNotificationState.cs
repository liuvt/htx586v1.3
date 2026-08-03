using HTX586CONTRACT.Application.Abstractions;

namespace HTX586CONTRACT.Web.Services;

public sealed class DriverNotificationState(IDriverNotificationService service)
{
    public int UnreadCount { get; private set; }
    public event Action? Changed;

    public async Task RefreshAsync(string driverId, CancellationToken cancellationToken = default)
    {
        UnreadCount = string.IsNullOrWhiteSpace(driverId)
            ? 0
            : await service.GetUnreadCountAsync(driverId, cancellationToken);
        Changed?.Invoke();
    }
}
