using Microsoft.EntityFrameworkCore;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Enums;

namespace VehicleBooking.Web.Domain.Services;

public sealed class DriverBookingNotificationService : IDriverBookingNotificationService
{
    private readonly VehicleBookingDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IWebPushSender _webPush;
    private readonly ILogger<DriverBookingNotificationService> _logger;

    public DriverBookingNotificationService(
        VehicleBookingDbContext db,
        INotificationService notifications,
        IWebPushSender webPush,
        ILogger<DriverBookingNotificationService> logger)
    {
        _db = db;
        _notifications = notifications;
        _webPush = webPush;
        _logger = logger;
    }

    public async Task NotifyNewAssignmentAsync(long bookingId, int driverId)
    {
        var (userId, booking) = await LoadAsync(driverId, bookingId);
        if (userId is null || booking is null) return;

        var start = booking.StartAtUtc.ToLocalTime();
        var title = "มีงานใหม่สำหรับคุณ";
        var message = $"งาน #{bookingId} วันที่ {start:dd/MM/yyyy HH:mm} จาก {booking.PickupLocation}";
        var url = $"/driver/detail/{bookingId}";

        await CreateSafeAsync(userId.Value, driverId, bookingId, NotificationType.NewDriverAssignment, title, message, url);
        await PushSafeAsync(userId.Value, title, message, url);
    }

    public async Task NotifyAdminReassignedToNewDriverAsync(long bookingId, int? oldDriverId, int newDriverId)
    {
        var (userId, booking) = await LoadAsync(newDriverId, bookingId);
        if (userId is null || booking is null) return;

        var start = booking.StartAtUtc.ToLocalTime();
        var title = "ผู้ดูแลมอบหมายงานให้คุณ";
        var message = $"งาน #{bookingId} วันที่ {start:dd/MM/yyyy HH:mm} จาก {booking.PickupLocation}";
        var url = $"/driver/detail/{bookingId}";

        await CreateSafeAsync(userId.Value, newDriverId, bookingId, NotificationType.AdminDriverReassignedToYou, title, message, url);
        await PushSafeAsync(userId.Value, title, message, url);
    }

    public async Task NotifyAdminReassignedAwayFromOldDriverAsync(long bookingId, int oldDriverId, int newDriverId)
    {
        var (userId, booking) = await LoadAsync(oldDriverId, bookingId);
        if (userId is null || booking is null) return;

        var start = booking.StartAtUtc.ToLocalTime();
        var title = "งานของคุณถูกมอบหมายใหม่";
        var message = $"งาน #{bookingId} วันที่ {start:dd/MM/yyyy HH:mm} ถูกมอบหมายให้พนักงานขับรถท่านอื่น";
        var url = "/driver/jobs";

        await CreateSafeAsync(userId.Value, oldDriverId, bookingId, NotificationType.AdminDriverReassignedAwayFromYou, title, message, url);
        await PushSafeAsync(userId.Value, title, message, url);
    }

    public async Task NotifyBookingCancelledAsync(long bookingId, int driverId)
    {
        var (userId, booking) = await LoadAsync(driverId, bookingId);
        if (userId is null || booking is null) return;

        var start = booking.StartAtUtc.ToLocalTime();
        var title = "งานถูกยกเลิก";
        var message = $"งาน #{bookingId} วันที่ {start:dd/MM/yyyy HH:mm} ถูกยกเลิกแล้ว";
        var url = "/driver/jobs";

        await CreateSafeAsync(userId.Value, driverId, bookingId, NotificationType.BookingCancelled, title, message, url);
        await PushSafeAsync(userId.Value, title, message, url);
    }

    public async Task NotifyBookingUpdatedAsync(long bookingId, int driverId)
    {
        var (userId, booking) = await LoadAsync(driverId, bookingId);
        if (userId is null || booking is null) return;

        var start = booking.StartAtUtc.ToLocalTime();
        var title = "รายละเอียดงานมีการเปลี่ยนแปลง";
        var message = $"งาน #{bookingId} วันที่ {start:dd/MM/yyyy HH:mm} มีการแก้ไขข้อมูล";
        var url = $"/driver/detail/{bookingId}";

        await CreateSafeAsync(userId.Value, driverId, bookingId, NotificationType.BookingUpdated, title, message, url);
        await PushSafeAsync(userId.Value, title, message, url);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<(int? UserId, Booking? Booking)> LoadAsync(int driverId, long bookingId)
    {
        var driver = await _db.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver is null)
        {
            _logger.LogWarning("DriverBookingNotification: driver {DriverId} not found", driverId);
            return (null, null);
        }

        var booking = await _db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.BookingId == bookingId);
        if (booking is null)
        {
            _logger.LogWarning("DriverBookingNotification: booking {BookingId} not found", bookingId);
            return (null, null);
        }

        return (driver.UserId, booking);
    }

    private async Task CreateSafeAsync(int userId, int driverId, long bookingId, NotificationType type,
        string title, string message, string url)
    {
        try
        {
            await _notifications.CreateAsync(userId, driverId, bookingId, type, title, message, url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create notification for driver {DriverId}, booking {BookingId}", driverId, bookingId);
        }
    }

    private async Task PushSafeAsync(int userId, string title, string message, string url)
    {
        try
        {
            await _webPush.SendToUserAsync(userId, title, message, url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send web push to user {UserId}", userId);
        }
    }
}
