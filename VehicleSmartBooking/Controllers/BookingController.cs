using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using System.Security.Claims;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Enums;
using VehicleBooking.Web.Domain.Helpers;

namespace VehicleSmartBooking.Controllers
{
    [Authorize(Roles = "User,Approver,Admin")]
    public class BookingController : Controller
    {
        private readonly VehicleBookingDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<BookingController> _logger;

        public BookingController(VehicleBookingDbContext db, IWebHostEnvironment env, ILogger<BookingController> logger)
        {
            _db = db;
            _env = env;
            _logger = logger;
        }

        public class BookingFormModel
        {
            // Requester fields (optional if authenticated) 
            public string? EmpCode { get; set; }
            public string? FullName { get; set; }
            public string? Department { get; set; }
            public string? Phone { get; set; }

            // Booking meta
            public string? BookingMode { get; set; } // "in" | "out" | "personal"
            public string? VehicleType { get; set; } // "pickup","van","sedan"

            // Details
            public string? RefNo { get; set; }
            public string? StartAt { get; set; }
            public string? EndAt { get; set; }
            public string? PickupPoint { get; set; }
            public string? Destination { get; set; }
            public string? Purpose { get; set; }
            public int? PassengerCount { get; set; }
            public string? OtherDetail { get; set; }

            // optional extras
            public string? OutTripType { get; set; }
            public string? CostCenter { get; set; }
            public string? SONo { get; set; }
        }

        public IActionResult Create()
        {
            ViewData["ActiveNav"] = "MyRequest";

            // Build approval preview for UI
            var userCode = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userCode))
            {
                var requester = _db.Users.AsNoTracking().SingleOrDefault(u => u.UserCode == userCode && u.IsActive);
                if (requester != null)
                {
                    ViewBag.Users = requester;
                    var preview = BuildApprovalChainAsync(requester.UserId).GetAwaiter().GetResult();
                    ViewBag.ApprovalPreview = preview.Approvers
                        .Select(a => new
                        {
                            a.LevelNo,
                            Name = a.User.UsernameTH ?? a.User.UsernameEN ?? a.User.UserCode,
                            Position = a.User.PositionEN ?? ""
                        })
                        .ToList();

                    if (preview.IsAutoApproved)
                    {
                        ViewBag.ApprovalPreviewNote = "No approval required for your position.";
                    }
                    else if (preview.IsAdminActionRequired)
                    {
                        ViewBag.ApprovalPreviewNote = "Approval chain not found. Admin action required.";
                    }
                }
            }

            return View();
        }

        // MyBookings: list view for "My Task"
        [Authorize]
        [HttpGet("/booking/mybookings")]
        public async Task<IActionResult> MyBookings()
        {
            ViewData["ActiveNav"] = "MyTask";

            var userCodeClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userCodeClaim))
            {
                return View(new List<Booking>());
            }

            var currentUser = await _db.Users.SingleOrDefaultAsync(u => u.UserCode == userCodeClaim && u.IsActive);
            if (currentUser is null)
            {
                return View(new List<Booking>());
            }

            var userId = currentUser.UserId;

            var bookings = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.Requester)
                .Where(b =>
                    b.RequesterUserId == userId ||
                    b.AssignedDriverId == userId ||
                    b.Approvals.Any(a => a.ApproverUserId == userId))
                .OrderByDescending(b => b.StartAtUtc)
                .ToListAsync();

            return View(bookings);
        }
        
        [Authorize]
        public async Task<IActionResult> Detail(long id)
        {
            var booking = await _db.Bookings
                .Include(b => b.Requester)
                .Include(b => b.Attachments)
                .Include(b => b.Approvals)
                .ThenInclude(a => a.Approver)
                .Include(b => b.ExternalRental)
                // include assigned driver and vehicle to show details in view
                .Include(b => b.AssignedDriver)
                    .ThenInclude(d => d.User)
                .Include(b => b.AssignedVehicle)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking is null) return NotFound();

            // mark layout active nav for Detail -> MyTask
            ViewData["ActiveNav"] = "MyTask";

            return View(booking);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] BookingFormModel model, List<IFormFile>? Attachments)
        {
            ViewData["ActiveNav"] = "MyRequest";
            if (!ModelState.IsValid) return View(model);

            var tripType = model.BookingMode?.ToLower() == "out"
                ? TripType.OutProvince
                : TripType.InProvince;

            var vehicleType = (model.VehicleType ?? "").ToLower() switch
            {
                "pickup" => VehicleType.Pickup,
                "van" => VehicleType.Van,
                _ => VehicleType.Sedan
            };

            if (!DateTime.TryParse(model.StartAt, out var startLocal) ||
                !DateTime.TryParse(model.EndAt, out var endLocal))
            {
                ModelState.AddModelError("", "Invalid date/time");
                return View(model);
            }

            var startUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime();
            var endUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime();
            if (endUtc <= startUtc)
            {
                ModelState.AddModelError("", "End must be after start");
                return View(model);
            }

            var userCode = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userCode)) return Forbid();

            var requester = await _db.Users.SingleAsync(u => u.UserCode == userCode && u.IsActive);

            // Use Serializable isolation to avoid race conditions when assigning drivers/vehicles
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                var now = DateTime.UtcNow;

                var booking = new Booking
                {
                    RequesterUserId = requester.UserId,
                    TripType = tripType,
                    VehicleTypeRequested = vehicleType,
                    StartAtUtc = startUtc,
                    EndAtUtc = endUtc,
                    PickupLocation = model.PickupPoint ?? "",
                    DestinationLocation = model.Destination ?? "",
                    RequesterPhone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim(),
                    Purpose = model.Purpose,
                    PassengerCount = model.PassengerCount,
                    DetailNote = model.OtherDetail,
                    JobNo = model.RefNo,
                    CostCenter = model.CostCenter,
                    SONo = model.SONo,
                    Status = BookingStatus.Submitted,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                _db.Bookings.Add(booking);
                await _db.SaveChangesAsync();

                await SaveAttachmentsAsync(booking.BookingId, requester.UserId, Attachments);

                // attempt assignment but do not let assignment errors prevent booking creation
                var assignResult = (Assigned: false, VehicleId: (int?)null, DriverId: (int?)null);
                try
                {
                    assignResult = await TryAssignFirstAvailableCompanyVehicleAsync(
                        booking.BookingId, vehicleType, startUtc, endUtc);
                }
                catch (Exception ex)
                {
                    // log and fallback to vendor flow
                    _logger.LogError(ex, "Error while trying to assign vehicle for booking {BookingId}", booking.BookingId);
                    assignResult = (false, null, null);
                }

                if (!assignResult.Assigned)
                {
                    // no company vehicle available -> vendor flow
                    booking.Status = BookingStatus.WaitingAdminVendorQuotation;
                    booking.IsExternalRental = true;
                }
                else
                {
                    // Final availability check to avoid race: ensure candidate still free
                    var nowBusy = await _db.Bookings.AsNoTracking().AnyAsync(b =>
                        b.BookingId != booking.BookingId &&
                        !BookingStatusHelper.TerminalStatuses.Contains(b.Status) &&
                        b.StartAtUtc < endUtc &&
                        startUtc < b.EndAtUtc &&
                        ((b.AssignedVehicleId != null && b.AssignedVehicleId == assignResult.VehicleId) ||
                         (b.AssignedDriverId != null && b.AssignedDriverId == assignResult.DriverId)));

                    if (nowBusy)
                    {
                        // candidate became busy -> fallback to vendor flow
                        booking.Status = BookingStatus.WaitingAdminVendorQuotation;
                        booking.IsExternalRental = true;
                    }
                    else
                    {
                        // persist assignment on booking and update driver load
                        booking.AssignedVehicleId = assignResult.VehicleId;
                        booking.AssignedDriverId = assignResult.DriverId;
                        booking.IsExternalRental = false;

                        var driverToUpdate = await _db.Drivers.SingleOrDefaultAsync(d => d.DriverId == assignResult.DriverId);
                        if (driverToUpdate != null)
                        {
                            driverToUpdate.LastAssignedAtUtc = DateTime.UtcNow;
                        }

                        if (tripType == TripType.InProvince)
                        {
                            booking.Status = BookingStatus.WaitingDriverAccept;

                            _db.BookingDispatchLogs.Add(new BookingDispatchLog
                            {
                                BookingId = booking.BookingId,
                                AttemptNo = 1,
                                VehicleId = assignResult.VehicleId!.Value,
                                DriverId = assignResult.DriverId!.Value,
                                DispatchedAtUtc = now
                            });
                        }
                        else
                        {
                            booking.Status = BookingStatus.WaitingApproval;
                            await CreateApprovalsFromLineManagerChainAsync(booking.BookingId, requester.UserId);
                        }
                    }
                }

                booking.UpdatedAtUtc = now;
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return RedirectToAction(nameof(Detail), new { id = booking.BookingId });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        private async Task SaveAttachmentsAsync(long bookingId, int uploadedByUserId, List<IFormFile>? files)
        {
            if (files == null || files.Count == 0) return;

            var uploadsRoot = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "bookings", bookingId.ToString());
            Directory.CreateDirectory(uploadsRoot);

            foreach (var file in files)
            {
                if (file == null || file.Length == 0) continue;

                var safeFileName = Path.GetFileName(file.FileName);
                var destPath = Path.Combine(uploadsRoot, safeFileName);

                var finalPath = destPath;
                var idx = 1;
                while (System.IO.File.Exists(finalPath))
                {
                    var name = Path.GetFileNameWithoutExtension(safeFileName);
                    var ext = Path.GetExtension(safeFileName);
                    finalPath = Path.Combine(uploadsRoot, $"{name}-{idx}{ext}");
                    idx++;
                }

                await using (var stream = System.IO.File.Create(finalPath))
                {
                    await file.CopyToAsync(stream);
                }

                var storagePath = $"/uploads/bookings/{bookingId}/{Path.GetFileName(finalPath)}";

                _db.BookingAttachments.Add(new BookingAttachment
                {
                    BookingId = bookingId,
                    FileName = Path.GetFileName(finalPath),
                    ContentType = file.ContentType,
                    StoragePath = storagePath,
                    UploadedByUserId = uploadedByUserId
                });
            }

            await _db.SaveChangesAsync();
        }

        private async Task CreateApprovalsFromLineManagerChainAsync(long bookingId, int requesterUserId)
        {
            var now = DateTime.UtcNow;

            // if approvals already exist, do not create duplicates
            var alreadyHasApprovals = await _db.BookingApprovals.AsNoTracking().AnyAsync(a => a.BookingId == bookingId);
            if (alreadyHasApprovals) return;

            var chain = await BuildApprovalChainAsync(requesterUserId);
            if (chain.IsAutoApproved)
            {
                var bookingAuto = await _db.Bookings.SingleAsync(b => b.BookingId == bookingId);
                bookingAuto.Status = bookingAuto.IsExternalRental ? BookingStatus.WaitingAdminVendorConfirm : BookingStatus.WaitingDriverAccept;
                bookingAuto.UpdatedAtUtc = now;
                await _db.SaveChangesAsync();
                return;
            }

            if (chain.IsAdminActionRequired || chain.Approvers.Count == 0)
            {
                var booking = await _db.Bookings.SingleAsync(b => b.BookingId == bookingId);
                booking.Status = BookingStatus.AdminActionRequired;
                booking.UpdatedAtUtc = now;
                await _db.SaveChangesAsync();
                return;
            }

            _db.BookingApprovals.AddRange(
                chain.Approvers.Select(a => new BookingApproval
                {
                    BookingId = bookingId,
                    ApproverUserId = a.User.UserId,
                    LevelNo = a.LevelNo,
                    Status = ApprovalStatus.Pending,
                    CreatedAtUtc = now
                })
            );

            await _db.SaveChangesAsync();
        }

        private async Task<ApprovalChainResult> BuildApprovalChainAsync(int requesterUserId)
        {
            var requester = await _db.Users.AsNoTracking().SingleAsync(u => u.UserId == requesterUserId);

            ApproverRole GetRole(User? u)
            {
                if (u == null) return ApproverRole.Staff;
                var pos = (u.PositionEN ?? string.Empty).Trim();
                if (pos.StartsWith("Acting ", StringComparison.OrdinalIgnoreCase))
                {
                    pos = pos.Substring("Acting ".Length).Trim();
                }
                if (pos.Equals("Section Manager", StringComparison.OrdinalIgnoreCase)) return ApproverRole.SectionManager;
                if (pos.Equals("Division Manager", StringComparison.OrdinalIgnoreCase)) return ApproverRole.DM;
                if (pos.Equals("Vice President", StringComparison.OrdinalIgnoreCase)) return ApproverRole.VP;
                if (pos.Equals("Deputy Managing Director", StringComparison.OrdinalIgnoreCase)) return ApproverRole.DMD;
                if (pos.Equals("Managing Director", StringComparison.OrdinalIgnoreCase)) return ApproverRole.MD;
                return ApproverRole.Staff;
            }

            async Task<User?> GetManagerAsync(User u)
            {
                if (!u.LineManagerId.HasValue) return null;
                return await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == u.LineManagerId.Value);
            }

            var requesterRole = GetRole(requester);
            var lm1 = await GetManagerAsync(requester);
            var lm2 = lm1 != null ? await GetManagerAsync(lm1) : null;
            var lm3 = lm2 != null ? await GetManagerAsync(lm2) : null;
            var lm4 = lm3 != null ? await GetManagerAsync(lm3) : null;

            var approvals = new List<ApprovalCandidate>();

            if (requesterRole == ApproverRole.DMD || requesterRole == ApproverRole.MD)
            {
                return new ApprovalChainResult(approvals, IsAutoApproved: true, IsAdminActionRequired: false);
            }

            // secretary case: line manager is DMD
            if (requesterRole == ApproverRole.Staff && GetRole(lm1) == ApproverRole.DMD)
            {
                approvals.Add(new ApprovalCandidate(lm1!, 1));
            }
            else if (requesterRole == ApproverRole.DM)
            {
                if (GetRole(lm1) == ApproverRole.VP) approvals.Add(new ApprovalCandidate(lm1!, 1));
                if (GetRole(lm2) == ApproverRole.DMD) approvals.Add(new ApprovalCandidate(lm2!, 2));
            }
            else if (requesterRole == ApproverRole.VP)
            {
                if (GetRole(lm1) == ApproverRole.DMD) approvals.Add(new ApprovalCandidate(lm1!, 1));
            }
            else
            {
                // normal staff flow, handle Section Manager between requester and DM
                var dm = lm1;
                var vp = lm2;
                var dmd = lm3;

                if (GetRole(lm1) == ApproverRole.SectionManager)
                {
                    dm = lm2;
                    vp = lm3;
                    dmd = lm4;
                }

                if (GetRole(dm) == ApproverRole.DM) approvals.Add(new ApprovalCandidate(dm!, 1));
                if (GetRole(vp) == ApproverRole.VP) approvals.Add(new ApprovalCandidate(vp!, 2));
                if (GetRole(dmd) == ApproverRole.DMD) approvals.Add(new ApprovalCandidate(dmd!, 3));
            }

            if (approvals.Count == 0)
            {
                return new ApprovalChainResult(approvals, IsAutoApproved: false, IsAdminActionRequired: true);
            }

            return new ApprovalChainResult(approvals, IsAutoApproved: false, IsAdminActionRequired: false);
        }

        private sealed record ApprovalCandidate(User User, int LevelNo);
        private sealed record ApprovalChainResult(List<ApprovalCandidate> Approvers, bool IsAutoApproved, bool IsAdminActionRequired);

        private enum ApproverRole
        {
            Staff = 0,
            SectionManager = 1,
            DM = 2,
            VP = 3,
            DMD = 4,
            MD = 5
        }

        public sealed class QueueVm
        {
            public VehicleType VehicleType { get; set; }
            public DateTime StartDate { get; set; }
            public int Days { get; set; }
            public List<VehicleScheduleVm> Vehicles { get; set; } = new();
        }

        public sealed class VehicleScheduleVm
        {
            public Vehicle Vehicle { get; set; } = null!;
            public List<BookingSlotVm> Slots { get; set; } = new();
        }

        public sealed class BookingSlotVm
        {
            public long BookingId { get; set; }
            public DateTime StartLocal { get; set; }
            public DateTime EndLocal { get; set; }
            public string? RequesterName { get; set; }
            public string? DisplayLabel { get; set; }
            public bool IsExternalRental { get; set; }
        }

        [Authorize]
        [HttpGet("/booking/availability")]
        public async Task<IActionResult> CheckAvailability([FromQuery] string? vehicleType, [FromQuery] string? startAt, [FromQuery] string? endAt)
        {
            var type = (vehicleType ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "pickup" => VehicleType.Pickup,
                "van" => VehicleType.Van,
                "sedan" => VehicleType.Sedan,
                _ => (VehicleType?)null
            };

            if (!type.HasValue)
            {
                return Ok(new { ok = false, message = "กรุณาเลือกประเภทรถ" });
            }

            if (!DateTime.TryParse(startAt, out var startLocal) ||
                !DateTime.TryParse(endAt, out var endLocal))
            {
                return Ok(new { ok = false, message = "กรุณาเลือกช่วงเวลาให้ครบถ้วน" });
            }

            if (endLocal <= startLocal)
            {
                return Ok(new { ok = false, message = "เวลาสิ้นสุดต้องมากกว่าเวลาเริ่มต้น" });
            }

            var startUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime();
            var endUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime();

            var candidates = await (
                from d in _db.Drivers.AsNoTracking()
                join v in _db.Vehicles.AsNoTracking() on d.VehicleId equals v.VehicleId
                where d.IsActive
                      && v.IsActive
                      && v.VehicleType == type.Value
                select new { d.DriverId, d.VehicleId }
            ).ToListAsync();

            if (candidates.Count == 0)
            {
                return Ok(new { ok = true, total = 0, available = 0 });
            }

            var vehicleIds = candidates.Select(x => x.VehicleId).ToList();
            var driverIds = candidates.Select(x => x.DriverId).ToList();
            var terminalStatuses = BookingStatusHelper.TerminalStatuses;

            var busyAssignments = await _db.Bookings
                .AsNoTracking()
                .Where(b =>
                    !terminalStatuses.Contains(b.Status) &&
                    !b.IsExternalRental &&
                    b.StartAtUtc < endUtc &&
                    startUtc < b.EndAtUtc &&
                    ((b.AssignedVehicleId != null && vehicleIds.Contains(b.AssignedVehicleId.Value)) ||
                     (b.AssignedDriverId != null && driverIds.Contains(b.AssignedDriverId.Value))))
                .Select(b => new { b.AssignedVehicleId, b.AssignedDriverId })
                .ToListAsync();

            var busyVehicles = new HashSet<int>(busyAssignments.Where(x => x.AssignedVehicleId.HasValue).Select(x => x.AssignedVehicleId!.Value));
            var busyDrivers = new HashSet<int>(busyAssignments.Where(x => x.AssignedDriverId.HasValue).Select(x => x.AssignedDriverId!.Value));

            var available = candidates.Count(c => !busyVehicles.Contains(c.VehicleId) && !busyDrivers.Contains(c.DriverId));

            var pendingUnassignedCount = await _db.Bookings
                .AsNoTracking()
                .Where(b =>
                    !terminalStatuses.Contains(b.Status) &&
                    !b.IsExternalRental &&
                    b.AssignedVehicleId == null &&
                    b.AssignedDriverId == null &&
                    b.VehicleTypeRequested == type.Value &&
                    b.StartAtUtc < endUtc &&
                    startUtc < b.EndAtUtc)
                .CountAsync();

            if (pendingUnassignedCount > 0)
            {
                available = Math.Max(0, available - pendingUnassignedCount);
            }

            return Ok(new { ok = true, total = candidates.Count, available });
        }

        private async Task<(bool Assigned, int? VehicleId, int? DriverId)> TryAssignFirstAvailableCompanyVehicleAsync(long bookingId,VehicleType vehicleType,DateTime startUtc,DateTime endUtc)
        {
            // 1) candidate: driver active + vehicle active + type ตรง
            var candidates = await (
                from d in _db.Drivers.AsNoTracking()
                join v in _db.Vehicles.AsNoTracking() on d.VehicleId equals v.VehicleId
                where d.IsActive
                      && v.IsActive
                      && v.VehicleType == vehicleType
                orderby d.LastAssignedAtUtc, d.DriverId
                select new { d.DriverId, d.VehicleId }
            ).ToListAsync();

            if (!candidates.Any())
                return (false, null, null);

            // 2) check each candidate against existing non-terminal overlapping bookings
            var terminal = BookingStatusHelper.TerminalStatuses;

            foreach (var cand in candidates)
            {
                var isBusy = await _db.Bookings.AsNoTracking().AnyAsync(b =>
                    b.BookingId != bookingId &&
                    !terminal.Contains(b.Status) &&
                    b.StartAtUtc < endUtc &&
                    startUtc < b.EndAtUtc &&
                    ((b.AssignedVehicleId != null && b.AssignedVehicleId == cand.VehicleId) ||
                     (b.AssignedDriverId != null && b.AssignedDriverId == cand.DriverId)));

                if (isBusy) continue;

                // found available candidate, return without persisting
                return (true, cand.VehicleId, cand.DriverId);
            }

            return (false, null, null);
        }

        [Authorize]
        [HttpPost("/booking/accept-vendor/{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptVendor(long id)
        {
            var booking = await _db.Bookings.Include(b => b.ExternalRental).SingleOrDefaultAsync(b => b.BookingId == id);
            if (booking == null) return NotFound();

            // only requester can accept vendor
            var userCode = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userCode)) return Forbid();
            var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserCode == userCode && u.IsActive);
            if (user == null || user.UserId != booking.RequesterUserId) return Forbid();

            if (booking.ExternalRental == null)
            {
                TempData["Error"] = "No vendor quote available.";
                return RedirectToAction(nameof(Detail), new { id });
            }

            // mark user decision accepted
            booking.ExternalRental.UserDecision = ExternalUserDecision.Accepted;
            booking.ExternalRental.UserDecisionAtUtc = DateTime.UtcNow;
            booking.UpdatedAtUtc = DateTime.UtcNow;

            // move to approval flow
            booking.Status = BookingStatus.WaitingApproval;
            await _db.SaveChangesAsync();

            // create approvals (reuse existing helper)
            await CreateApprovalsFromLineManagerChainAsync(booking.BookingId, booking.RequesterUserId);

            TempData["Success"] = "You accepted the vendor quote. The request will go through approval process.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        [Authorize]
        [HttpPost("/booking/reject-vendor/{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectVendor(long id, string? reason)
        {
            var booking = await _db.Bookings.Include(b => b.ExternalRental).SingleOrDefaultAsync(b => b.BookingId == id);
            if (booking == null) return NotFound();

            var userCode = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userCode)) return Forbid();
            var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserCode == userCode && u.IsActive);
            if (user == null || user.UserId != booking.RequesterUserId) return Forbid();

            if (booking.ExternalRental == null)
            {
                TempData["Error"] = "No vendor quote available.";
                return RedirectToAction(nameof(Detail), new { id });
            }

            booking.ExternalRental.UserDecision = ExternalUserDecision.Rejected;
            booking.ExternalRental.UserDecisionAtUtc = DateTime.UtcNow;
            booking.UpdatedAtUtc = DateTime.UtcNow;
            booking.Status = BookingStatus.VendorRejectedByUser;

            await _db.SaveChangesAsync();

            TempData["Success"] = "You rejected the vendor quote.";
            return RedirectToAction(nameof(Detail), new { id });
        }
    }
}
