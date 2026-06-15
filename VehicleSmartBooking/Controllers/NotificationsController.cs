using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VehicleBooking.Web.Domain.Enums;
using VehicleBooking.Web.Domain.Services;

namespace VehicleSmartBooking.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly INotificationService _notifications;
    private readonly ICurrentUserService _currentUser;

    public NotificationsController(INotificationService notifications, ICurrentUserService currentUser)
    {
        _notifications = notifications;
        _currentUser = currentUser;
    }

    // GET /Notifications
    public async Task<IActionResult> Index()
    {
        ViewData["ActiveNav"] = "Notifications";
        var user = await _currentUser.GetCurrentUserAsync(User);
        if (user is null) return Forbid();
        var items = await _notifications.GetRecentAsync(user.UserId, take: 50);
        return View(items);
    }

    // POST /Notifications/MarkAllRead
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var user = await _currentUser.GetCurrentUserAsync(User);
        if (user is null) return Forbid();
        await _notifications.MarkAllAsReadAsync(user.UserId);
        TempData["Success"] = "ทำเครื่องหมายอ่านแล้วทั้งหมด";
        return RedirectToAction(nameof(Index));
    }

    // GET /Notifications/MarkReadAndRedirect/123
    public async Task<IActionResult> MarkReadAndRedirect(long id)
    {
        var user = await _currentUser.GetCurrentUserAsync(User);
        if (user is null) return Forbid();
        var url = await _notifications.MarkAsReadAsync(id, user.UserId);
        if (!string.IsNullOrWhiteSpace(url))
            return Redirect(url);
        return RedirectToAction(nameof(Index));
    }

    // GET /Notifications/GetUnreadCount  (JSON for bell badge)
    public async Task<IActionResult> GetUnreadCount()
    {
        var user = await _currentUser.GetCurrentUserAsync(User);
        if (user is null) return Json(new { count = 0 });
        var count = await _notifications.GetUnreadCountAsync(user.UserId);
        return Json(new { count });
    }

    // GET /Notifications/GetRecent  (JSON for bell dropdown)
    public async Task<IActionResult> GetRecent()
    {
        var user = await _currentUser.GetCurrentUserAsync(User);
        if (user is null) return Json(new { items = Array.Empty<object>() });
        var items = await _notifications.GetRecentAsync(user.UserId, take: 10);
        return Json(new
        {
            items = items.Select(n => new
            {
                n.NotificationId,
                n.Title,
                n.Message,
                n.Url,
                n.IsRead,
                createdAtUtc = DateTime.SpecifyKind(n.CreatedAtUtc, DateTimeKind.Utc).ToString("o"),
            })
        });
    }

    // POST /Notifications/CreateTest  (manual test helper)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTest()
    {
        var user = await _currentUser.GetCurrentUserAsync(User);
        if (user is null) return Forbid();
        await _notifications.CreateAsync(
            user.UserId, null, null,
            NotificationType.TestNotification,
            "ทดสอบการแจ้งเตือน",
            "นี่คือข้อความทดสอบการแจ้งเตือนในระบบ Vehicle Smart Booking",
            "/Notifications");
        TempData["Success"] = "สร้างการแจ้งเตือนทดสอบแล้ว";
        return RedirectToAction(nameof(Index));
    }
}
