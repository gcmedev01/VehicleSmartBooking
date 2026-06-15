using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Enums;

namespace VehicleBooking.Web.Domain.Services;

public interface INotificationService
{
    Task CreateAsync(int userId, int? driverId, long? bookingId, NotificationType type, string title, string message, string? url);
    Task<int> GetUnreadCountAsync(int userId);
    Task<IReadOnlyList<Notification>> GetRecentAsync(int userId, int take = 10);
    /// <summary>Marks notification as read and returns its Url (or null). Validates that notificationId belongs to userId.</summary>
    Task<string?> MarkAsReadAsync(long notificationId, int userId);
    Task MarkAllAsReadAsync(int userId);
}
