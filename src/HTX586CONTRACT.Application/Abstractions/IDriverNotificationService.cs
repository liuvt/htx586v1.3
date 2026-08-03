using HTX586CONTRACT.Application.Notifications;

namespace HTX586CONTRACT.Application.Abstractions;

public interface IDriverNotificationService
{
    Task<IReadOnlyList<DriverNotificationDto>> GetAsync(
        string driverId,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        string driverId,
        CancellationToken cancellationToken = default);

    Task MarkReadAsync(
        Guid notificationId,
        string driverId,
        CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(
        string driverId,
        CancellationToken cancellationToken = default);
}
