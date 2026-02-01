using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VehicleBooking.Web.Data;

namespace VehicleSmartBooking.Controllers
{
    [Authorize(Roles = "Approver,Admin")]
    public class ApprovalsController : Controller
    {
        private readonly VehicleBookingDbContext _db;
        private readonly ILogger<ApprovalsController> _logger;

        public ApprovalsController(VehicleBookingDbContext db, ILogger<ApprovalsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // GET: /Approvals/Pending
        [HttpGet]
        public async Task<IActionResult> Pending()
        {
            ViewData["ActiveNav"] = "Approve";

            // TODO:
            // - resolve current userId
            // - list approvals where ApproverUserId == me AND Status == Pending
            await Task.CompletedTask;
            return View();
        }

        // GET: /Approvals/History
        [HttpGet]
        public async Task<IActionResult> History()
        {
            ViewData["ActiveNav"] = "ApproveHistory";

            // TODO:
            // - approvals by current approver, status approved/rejected, order by ActionAtUtc desc
            await Task.CompletedTask;
            return View();
        }

        // GET: /Approvals/Detail/{bookingId}
        [HttpGet]
        public async Task<IActionResult> Detail(long bookingId)
        {
            ViewData["ActiveNav"] = "Approve";

            // TODO:
            // - load booking + approvals to show decision context
            await Task.CompletedTask;
            return View();
        }

        // POST: /Approvals/Approve/{bookingId}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(long bookingId, string? comment)
        {
            // TODO:
            // - mark current level approval row as Approved
            // - if next level exists -> keep BookingStatus = WaitingApproval
            // - if final level approved -> set booking status -> WaitingDriverAccept (or next step)
            await Task.CompletedTask;
            return RedirectToAction(nameof(Detail), new { bookingId });
        }

        // POST: /Approvals/Reject/{bookingId}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(long bookingId, string comment)
        {
            // TODO:
            // - mark current level approval row as Rejected
            // - set booking status -> Rejected (terminal)
            await Task.CompletedTask;
            return RedirectToAction(nameof(Detail), new { bookingId });
        }
    }
}
