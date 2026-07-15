using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Enums;
using VehicleBooking.Web.Domain.Options;
using VehicleBooking.Web.Domain.Services;
using Microsoft.Extensions.Options;

namespace VehicleSmartBooking.Controllers;

[Authorize]
[IgnoreAntiforgeryToken]
public class PushNotificationsController : Controller
{
    private readonly INotificationService _notifications;
    private readonly IWebPushSender _webPush;
    private readonly ICurrentUserService _currentUser;
    private readonly VehicleBookingDbContext _db;
    private readonly VapidOptions _vapid;

    public PushNotificationsController(
        INotificationService notifications,
        IWebPushSender webPush,
        ICurrentUserService currentUser,
        VehicleBookingDbContext db,
        IOptions<VapidOptions> vapid)
    {
        _notifications = notifications;
        _webPush = webPush;
        _currentUser = currentUser;
        _db = db;
        _vapid = vapid.Value;
    }

    // GET /PushNotifications/PublicKey
    [HttpGet]
    public IActionResult PublicKey()
    {
        if (!_vapid.IsConfigured)
            return Json(new { enabled = false, publicKey = (string?)null });
        return Json(new { enabled = true, publicKey = _vapid.PublicKey });
    }

    // POST /PushNotifications/Subscribe
    [HttpPost]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Endpoint)
            || string.IsNullOrWhiteSpace(request.P256dh) || string.IsNullOrWhiteSpace(request.Auth))
            return Json(new { success = false, message = "ข้อมูล subscription ไม่ครบถ้วน" });

        var user = await _currentUser.GetCurrentUserAsync(User);
        if (user is null) return Json(new { success = false, message = "ไม่พบข้อมูลผู้ใช้" });

        var driver = await _currentUser.GetCurrentDriverAsync(User);
        var now = DateTime.UtcNow;

        var existing = await _db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint);

        if (existing is not null)
        {
            // Reactivate and update
            existing.UserId = user.UserId;
            existing.DriverId = driver?.DriverId;
            existing.P256dh = request.P256dh;
            existing.Auth = request.Auth;
            existing.UserAgent = request.UserAgent;
            existing.IsActive = true;
            existing.DeactivatedAtUtc = null;
            await _db.SaveChangesAsync();
        }
        else
        {
            _db.PushSubscriptions.Add(new PushSubscription
            {
                UserId = user.UserId,
                DriverId = driver?.DriverId,
                Endpoint = request.Endpoint,
                P256dh = request.P256dh,
                Auth = request.Auth,
                UserAgent = request.UserAgent,
                IsActive = true,
                CreatedAtUtc = now,
            });
            await _db.SaveChangesAsync();
        }

        return Json(new { success = true });
    }

    // POST /PushNotifications/Unsubscribe
    [HttpPost]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Endpoint))
            return Json(new { success = false, message = "ไม่พบ endpoint" });

        var user = await _currentUser.GetCurrentUserAsync(User);
        if (user is null) return Json(new { success = false, message = "ไม่พบข้อมูลผู้ใช้" });

        var sub = await _db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint && s.UserId == user.UserId);

        if (sub is not null && sub.IsActive)
        {
            sub.IsActive = false;
            sub.DeactivatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return Json(new { success = true });
    }

    // POST /PushNotifications/Test
    [HttpPost]
    public async Task<IActionResult> Test()
    {
        var user = await _currentUser.GetCurrentUserAsync(User);
        if (user is null) return Json(new { success = false, message = "ไม่พบข้อมูลผู้ใช้" });

        var driver = await _currentUser.GetCurrentDriverAsync(User);
        const string title = "ทดสอบการแจ้งเตือน";
        const string message = "หากเห็นข้อความนี้ แสดงว่าเปิดการแจ้งเตือนสำเร็จแล้ว";
        const string url = "/Notifications";

        await _notifications.CreateAsync(user.UserId, driver?.DriverId, null,
            NotificationType.TestNotification, title, message, url);

        if (_webPush.IsEnabled)
        {
            var sent = await _webPush.SendToUserAsync(user.UserId, title, message, url);
            return Json(new
            {
                success = true,
                pushSent = sent > 0,
                message = sent > 0
                    ? "ส่งการทดสอบแจ้งเตือนแล้ว กรุณาตรวจสอบโทรศัพท์"
                    : "บันทึกการแจ้งเตือนในระบบแล้ว แต่ยังไม่พบอุปกรณ์ที่เปิดรับ push หรือส่งไม่สำเร็จ กรุณากดเปิดการแจ้งเตือนบนมือถืออีกครั้ง"
            });
        }

        return Json(new
        {
            success = true,
            pushSent = false,
            message = "บันทึกการแจ้งเตือนในระบบแล้ว (Web Push ยังไม่ได้ตั้งค่า VAPID)"
        });
    }

    public sealed class SubscribeRequest
    {
        public string Endpoint { get; set; } = null!;
        public string P256dh { get; set; } = null!;
        public string Auth { get; set; } = null!;
        public string? UserAgent { get; set; }
    }

    public sealed class UnsubscribeRequest
    {
        public string Endpoint { get; set; } = null!;
    }
}
