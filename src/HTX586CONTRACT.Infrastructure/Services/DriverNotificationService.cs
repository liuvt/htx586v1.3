using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Application.Notifications;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Infrastructure.Services;

public sealed class DriverNotificationService(
    IDbContextFactory<ApplicationDbContext> factory) : IDriverNotificationService
{
    public async Task<IReadOnlyList<DriverNotificationDto>> GetAsync(
        string driverId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(driverId))
            return [];

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.DriverNotifications.AsNoTracking()
            .Where(x => x.DriverId == driverId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .Select(x => new DriverNotificationDto
            {
                Id = x.Id,
                Type = x.Type,
                Title = x.Title,
                Message = x.Message,
                LinkUrl = x.LinkUrl,
                IsRead = x.IsRead,
                CreatedAt = x.CreatedAt,
                ReadAt = x.ReadAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(
        string driverId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(driverId))
            return 0;

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.DriverNotifications.AsNoTracking()
            .CountAsync(x => x.DriverId == driverId && !x.IsRead, cancellationToken);
    }

    public async Task MarkReadAsync(
        Guid notificationId,
        string driverId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.DriverNotifications
            .Where(x => x.Id == notificationId && x.DriverId == driverId && !x.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsRead, true)
                .SetProperty(x => x.ReadAt, DateTime.UtcNow), cancellationToken);
    }

    public async Task MarkAllReadAsync(
        string driverId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.DriverNotifications
            .Where(x => x.DriverId == driverId && !x.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsRead, true)
                .SetProperty(x => x.ReadAt, DateTime.UtcNow), cancellationToken);
    }
}
