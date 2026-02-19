using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Services;
using System.Diagnostics;

namespace VehicleSmartBooking.Controllers
{
    [Authorize(Roles = "Driver")]
    [Route("driver")]
    public class DriverController : Controller
    {
        private readonly VehicleBookingDbContext _db;
        private readonly ICurrentUserService _currentUser;
        private readonly IDriverWorkflowService _driverWorkflow;
        private readonly IEmailNotificationService _emailNotifications;
        private readonly ILogger<DriverController> _logger;

        public DriverController(
            VehicleBookingDbContext db,
            ICurrentUserService currentUser,
            IDriverWorkflowService driverWorkflow,
            IEmailNotificationService emailNotifications,
            ILogger<DriverController> logger)
        {
            _db = db;
            _currentUser = currentUser;
            _driverWorkflow = driverWorkflow;
            _emailNotifications = emailNotifications;
            _logger = logger;
        }

        // GET: /driver/jobs
        [HttpGet("jobs")]
        public async Task<IActionResult> MyJobs()
        {
            ViewData["ActiveNav"] = "DriverJobs";

            var driver = await _currentUser.GetCurrentDriverAsync(User);
            if (driver is null) return Forbid(); // user นี้ไม่ใช่ driver

            // งานที่ assign ให้ driver นี้
            var jobs = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.Requester)
                .Include(b => b.AssignedVehicle)
                .Include(b => b.AssignedDriver)
                    .ThenInclude(d => d.User)
                .Where(b => b.AssignedDriverId == driver.DriverId)
                .OrderByDescending(b => b.StartAtUtc)
                .ToListAsync();

            return View(jobs);
        }

        // GET: /driver/detail/5
        [HttpGet("detail/{id:long}")]
        public async Task<IActionResult> Detail(long id)
        {
            ViewData["ActiveNav"] = "DriverJobs";

            var driver = await _currentUser.GetCurrentDriverAsync(User);
            if (driver is null) return Forbid();

            var booking = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.Requester)
                .Include(b => b.Attachments)
                .Include(b => b.AssignedVehicle)
                .Include(b => b.AssignedDriver)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking is null) return NotFound();

            // กัน driver เปิดงานคนอื่น
            if (booking.AssignedDriverId != driver.DriverId) return Forbid();

            return View(booking);
        }

        // POST: /driver/accept/5
        [HttpPost("accept/{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(long id)
        {
            ViewData["ActiveNav"] = "DriverJobs";

            var driver = await _currentUser.GetCurrentDriverAsync(User);
            if (driver is null) return Forbid();

            await _driverWorkflow.AcceptAsync(id, driver); // ✅ ส่งให้ถูก

            var booking = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.Requester)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking != null)
            {
                var adminEmails = await _db.Users
                    .AsNoTracking()
                    .Where(u => (u.RoleFlags & 2) != 0 && u.Email != null)
                    .Select(u => u.Email!)
                    .ToListAsync();

                try
                {
                    await _emailNotifications.NotifyStatusChangedAsync(
                        booking,
                        adminEmails,
                        ownerEmail: null,
                        statusChangedAtUtc: booking.UpdatedAtUtc,
                        relativeUrl: $"/Admin/Detail/{booking.BookingId}");

                    await _emailNotifications.NotifyStatusChangedAsync(
                        booking,
                        Array.Empty<string>(),
                        booking.Requester?.Email,
                        statusChangedAtUtc: booking.UpdatedAtUtc,
                        relativeUrl: $"/Booking/Detail/{booking.BookingId}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send driver accept email for booking {BookingId}", booking.BookingId);
                }
            }

            return RedirectToAction(nameof(Detail), new { id });
        }

        // POST: /driver/complete/5
        [HttpPost("complete/{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(long id)
        {
            ViewData["ActiveNav"] = "DriverJobs";

            var driver = await _currentUser.GetCurrentDriverAsync(User);
            if (driver is null) return Forbid();

            await _driverWorkflow.CompleteAsync(id, driver);

            var booking = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.Requester)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking != null)
            {
                var adminEmails = await _db.Users
                    .AsNoTracking()
                    .Where(u => (u.RoleFlags & 2) != 0 && u.Email != null)
                    .Select(u => u.Email!)
                    .ToListAsync();

                try
                {
                    await _emailNotifications.NotifyStatusChangedAsync(
                        booking,
                        adminEmails,
                        ownerEmail: null,
                        statusChangedAtUtc: booking.UpdatedAtUtc,
                        relativeUrl: $"/Admin/Detail/{booking.BookingId}");

                    await _emailNotifications.NotifyActionRequiredAsync(
                        booking,
                        string.IsNullOrWhiteSpace(booking.Requester?.Email)
                            ? Array.Empty<string>()
                            : new[] { booking.Requester.Email },
                        "เสร็จสิ้นแล้ว กรุณาให้คะแนนคนขับ",
                        relativeUrl: $"/Booking/Rate/{booking.BookingId}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send driver complete email for booking {BookingId}", booking.BookingId);
                }
            }

            return RedirectToAction(nameof(Detail), new { id });
        }

        // POST: /driver/decline/5
        [HttpPost("decline/{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Decline(long id, string? reason)
        {
            ViewData["ActiveNav"] = "DriverJobs";

            var driver = await _currentUser.GetCurrentDriverAsync(User);
            if (driver is null) return Forbid();

            await _driverWorkflow.DeclineAsync(id, driver, reason);
            return RedirectToAction(nameof(MyJobs));
        }
    }

}
