using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Services;

namespace VehicleSmartBooking.Controllers
{
    [Authorize]
    [Route("driver")]
    public class DriverController : Controller
    {
        private readonly VehicleBookingDbContext _db;
        private readonly ICurrentUserService _currentUser;
        private readonly IDriverWorkflowService _driverWorkflow;

        public DriverController(
            VehicleBookingDbContext db,
            ICurrentUserService currentUser,
            IDriverWorkflowService driverWorkflow)
        {
            _db = db;
            _currentUser = currentUser;
            _driverWorkflow = driverWorkflow;
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
