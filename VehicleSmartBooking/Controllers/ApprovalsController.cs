using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Enums;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Services;

namespace VehicleSmartBooking.Controllers
{
    [Authorize(Roles = "Approver,Admin")]
    public class ApprovalsController : Controller
    {
        private readonly VehicleBookingDbContext _db;
        private readonly ILogger<ApprovalsController> _logger;
        private readonly IEmailNotificationService _emailNotifications;

        public ApprovalsController(VehicleBookingDbContext db, ILogger<ApprovalsController> logger, IEmailNotificationService emailNotifications)
        {
            _db = db;
            _logger = logger;
            _emailNotifications = emailNotifications;
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
                .Include(b => b.CompletionPhotos)
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
            else if (booking.VehicleTypeRequested == VehicleType.Electric)
            {
                booking.Status = BookingStatus.ApprovedSelfDrive;
            }
            else if (booking.IsPersonal)
            {
                booking.Status = BookingStatus.Completed;
            }
            else
            {
                // if booking uses external rental:
                // - special occasion: admin fills price and complete
                // - normal flow: admin confirms vendor details
                booking.Status = booking.IsExternalRental
                    ? (booking.SpecialOccasionType.HasValue ? BookingStatus.WaitingAdminVendorQuotation : BookingStatus.WaitingAdminVendorConfirm)
                    : BookingStatus.WaitingDriverAccept;
            }

            booking.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            try
            {
                var pendingApprovals = await _db.BookingApprovals
                    .AsNoTracking()
                    .Include(a => a.Approver)
                    .Where(a => a.BookingId == booking.BookingId && a.Status == ApprovalStatus.Pending && a.Approver.Email != null)
                    .ToListAsync();

                var nextLevel = pendingApprovals.Count == 0
                    ? (int?)null
                    : pendingApprovals.Min(a => a.LevelNo);

                var approverEmails = pendingApprovals
                    .Where(a => nextLevel.HasValue && a.LevelNo == nextLevel.Value)
                    .Select(a => a.Approver.Email!)
                    .ToList();

                if (approverEmails.Count > 0)
                {
                    await _emailNotifications.NotifyActionRequiredAsync(
                        booking,
                        approverEmails,
                        "กรุณาอนุมัติใบงาน",
                        relativeUrl: $"/Approval/Detail/{booking.BookingId}");
                }
                else
                {
                    var bookingWithRequester = await _db.Bookings
                        .AsNoTracking()
                        .Include(b => b.Requester)
                        .FirstOrDefaultAsync(b => b.BookingId == booking.BookingId);

                    if (bookingWithRequester != null)
                    {
                        var adminEmails = await _db.Users
                            .AsNoTracking()
                            .Where(u => (u.RoleFlags & 2) != 0 && u.Email != null)
                            .Select(u => u.Email!)
                            .ToListAsync();

                        if (!string.IsNullOrWhiteSpace(bookingWithRequester.Requester?.Email))
                        {
                            await _emailNotifications.NotifyStatusChangedAsync(
                                bookingWithRequester,
                                Array.Empty<string>(),
                                bookingWithRequester.Requester.Email,
                                statusChangedAtUtc: bookingWithRequester.UpdatedAtUtc,
                                relativeUrl: $"/Booking/Detail/{bookingWithRequester.BookingId}");
                        }

                        if (adminEmails.Count > 0)
                        {
                            if (bookingWithRequester.Status == BookingStatus.WaitingAdminVendorConfirm)
                            {
                                await _emailNotifications.NotifyActionRequiredAsync(
                                    bookingWithRequester,
                                    adminEmails,
                                    "กรุณายืนยันข้อมูลผู้ให้บริการ",
                                    relativeUrl: $"/Admin/Detail/{bookingWithRequester.BookingId}");
                            }
                            else
                            {
                                await _emailNotifications.NotifyStatusChangedAsync(
                                    bookingWithRequester,
                                    adminEmails,
                                    ownerEmail: null,
                                    statusChangedAtUtc: bookingWithRequester.UpdatedAtUtc,
                                    relativeUrl: $"/Admin/Detail/{bookingWithRequester.BookingId}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send approval notifications for booking {BookingId}", booking.BookingId);
            }

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
