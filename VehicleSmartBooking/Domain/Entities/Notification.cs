using VehicleBooking.Web.Domain.Enums;

namespace VehicleBooking.Web.Domain.Entities;

public sealed class Notification
{
    public long NotificationId { get; set; }
    public int UserId { get; set; }
    public int? DriverId { get; set; }
    public long? BookingId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? Url { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }

    public User User { get; set; } = null!;
}
