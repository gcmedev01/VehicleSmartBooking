using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Enums;
using VehicleBooking.Web.Domain.Entities;

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

        // GET: /Approval/pending
        [HttpGet("/Approval/pending")]
        public async Task<IActionResult> Pending()
        {
            ViewData["ActiveNav"] = "Approve";

            var me = await ResolveCurrentUserAsync();
            if (me is null) return Forbid();

            // Bookings that have an approval row assigned to current user and still pending
            var bookings = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.Requester)
                .Where(b => b.Approvals.Any(a => a.ApproverUserId == me.UserId && a.Status == ApprovalStatus.Pending))
                .OrderByDescending(b => b.StartAtUtc)
                .ToListAsync();

            return View(bookings);
        }

        // GET: /Approval/history
        [HttpGet("/Approval/history")]
        public async Task<IActionResult> History()
        {
            ViewData["ActiveNav"] = "ApproveHistory";

            var me = await ResolveCurrentUserAsync();
            if (me is null) return Forbid();

            // Get approval rows by this approver that are already acted (not pending)
            var approvalRows = await _db.BookingApprovals
                .AsNoTracking()
                .Where(a => a.ApproverUserId == me.UserId && a.Status != ApprovalStatus.Pending)
                .OrderByDescending(a => a.ActionAtUtc)
                .ToListAsync();

            var bookingIdsOrdered = approvalRows.Select(a => a.BookingId).Distinct().ToList();

            var bookings = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.Requester)
                .Where(b => bookingIdsOrdered.Contains(b.BookingId))
                .ToListAsync();

            // preserve ordering from bookingIdsOrdered
            var bookingsOrdered = bookingIdsOrdered
                .Select(id => bookings.FirstOrDefault(b => b.BookingId == id))
                .Where(b => b != null)
                .ToList()!;

            return View(bookingsOrdered);
        }

        // GET: /Approval/detail/{bookingId}
        [HttpGet("/Approval/detail/{bookingId:long}")]
        public async Task<IActionResult> Detail(long bookingId)
        {
            ViewData["ActiveNav"] = "Approve";

            // load booking + approvals to show decision context
            var booking = await _db.Bookings
                .Include(b => b.Requester)
                .Include(b => b.Attachments)
                .Include(b => b.Approvals)
                .ThenInclude(a => a.Approver)
                .Include(b => b.ExternalRental)
                .Include(b => b.AssignedDriver)
                    .ThenInclude(d => d.User)
                .Include(b => b.AssignedVehicle)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null) return NotFound();

            var me = await ResolveCurrentUserAsync();
            if (me is null) return Forbid();

            // Prepare approvals for viewbag with simple shape
            ViewBag.Approvals = booking.Approvals
                .OrderBy(a => a.LevelNo)
                .Select(a => new
                {
                    Level = a.LevelNo,
                    ApproverUserId = a.ApproverUserId,
                    ApproverName = a.Approver != null ? (a.Approver.UsernameTH ?? a.Approver.UsernameEN) : a.ApproverUserId.ToString(),
                    Status = a.Status.ToString(),
                    ActionAtUtc = a.ActionAtUtc,
                    Comment = a.Comment
                })
                .ToList();

            // Determine whether current user can act: must have a pending approval row for this booking
            var myApproval = booking.Approvals.FirstOrDefault(a => a.ApproverUserId == me.UserId);
            var hasPendingForMe = myApproval != null && myApproval.Status == ApprovalStatus.Pending;
            // Only allow action when booking still in waiting-approval state and approver has pending row
            ViewBag.CanAct = hasPendingForMe && booking.Status == BookingStatus.WaitingApproval;

            // expose my approval status to view for helpful messaging
            ViewBag.HasApprovalRow = myApproval != null;
            ViewBag.MyApprovalStatus = myApproval?.Status.ToString();

            return View(booking);
        }

        // POST: /Approvals/Approve/{bookingId}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(long bookingId, string? comment)
        {
            var me = await ResolveCurrentUserAsync();
            if (me is null) return Forbid();

            // find the pending approval row for this user and booking
            var approval = await _db.BookingApprovals
                .Where(a => a.BookingId == bookingId && a.ApproverUserId == me.UserId && a.Status == ApprovalStatus.Pending)
                .OrderBy(a => a.LevelNo)
                .FirstOrDefaultAsync();

            if (approval == null)
            {
                TempData["Error"] = "No pending approval found for you on this booking.";
                return RedirectToAction(nameof(Detail), new { bookingId });
            }

            approval.Status = ApprovalStatus.Approved;
            approval.ActionAtUtc = DateTime.UtcNow;
            approval.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

            // reload booking and approvals to decide booking status
            var booking = await _db.Bookings
                .Include(b => b.Approvals)
                .SingleOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToAction(nameof(Pending));
            }

            // if any approval rows still pending, keep booking in WaitingApproval, otherwise move to WaitingDriverAccept
            var anyPending = booking.Approvals.Any(a => a.Status == ApprovalStatus.Pending);
            if (anyPending)
            {
                booking.Status = BookingStatus.WaitingApproval;
            }
            else
            {
                // if booking uses external rental, after approvals it should return to admin to confirm vendor details
                booking.Status = booking.IsExternalRental ? BookingStatus.WaitingAdminVendorConfirm : BookingStatus.WaitingDriverAccept;
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = "Approved successfully.";
            return RedirectToAction(nameof(Detail), new { bookingId });
        }

        // POST: /Approvals/Reject/{bookingId}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(long bookingId, string comment)
        {
            var me = await ResolveCurrentUserAsync();
            if (me is null) return Forbid();

            if (string.IsNullOrWhiteSpace(comment))
            {
                TempData["Error"] = "Reject reason is required.";
                return RedirectToAction(nameof(Detail), new { bookingId });
            }

            var approval = await _db.BookingApprovals
                .Where(a => a.BookingId == bookingId && a.ApproverUserId == me.UserId && a.Status == ApprovalStatus.Pending)
                .OrderBy(a => a.LevelNo)
                .FirstOrDefaultAsync();

            if (approval == null)
            {
                TempData["Error"] = "No pending approval found for you on this booking.";
                return RedirectToAction(nameof(Detail), new { bookingId });
            }

            approval.Status = ApprovalStatus.Rejected;
            approval.ActionAtUtc = DateTime.UtcNow;
            approval.Comment = comment.Trim();

            var booking = await _db.Bookings.SingleOrDefaultAsync(b => b.BookingId == bookingId);
            if (booking != null)
            {
                booking.Status = BookingStatus.Rejected;
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = "Rejected successfully.";
            return RedirectToAction(nameof(Detail), new { bookingId });
        }

        private async Task<User?> ResolveCurrentUserAsync()
        {
            var userCode = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User?.FindFirst("UserCode")?.Value;

            if (string.IsNullOrWhiteSpace(userCode))
                return null;

            return await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserCode == userCode && u.IsActive);
        }
    }
}
