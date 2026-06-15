using Microsoft.EntityFrameworkCore;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Enums;

namespace VehicleBooking.Web.Domain.Services;

public sealed class NotificationService : INotificationService
{
    private readonly VehicleBookingDbContext _db;

    public NotificationService(VehicleBookingDbContext db) => _db = db;

    public async Task CreateAsync(int userId, int? driverId, long? bookingId, NotificationType type,
        string title, string message, string? url)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            DriverId = driverId,
            BookingId = bookingId,
            Type = type,
            Title = title,
            Message = message,
            Url = url,
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    public Task<int> GetUnreadCountAsync(int userId)
        => _db.Notifications.AsNoTracking()
               .CountAsync(n => n.UserId == userId && !n.IsRead);

    public async Task<IReadOnlyList<Notification>> GetRecentAsync(int userId, int take = 10)
        => await _db.Notifications.AsNoTracking()
               .Where(n => n.UserId == userId)
               .OrderByDescending(n => n.CreatedAtUtc)
               .Take(take)
               .ToListAsync();

    public async Task<string?> MarkAsReadAsync(long notificationId, int userId)
    {
        var n = await _db.Notifications
            .SingleOrDefaultAsync(x => x.NotificationId == notificationId && x.UserId == userId);
        if (n is null) return null;
        if (!n.IsRead)
        {
            n.IsRead = true;
            n.ReadAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return n.Url;
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        var now = DateTime.UtcNow;
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAtUtc, now));
    }
}
