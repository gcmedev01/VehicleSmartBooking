using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Enums;
using VehicleBooking.Web.Domain.Helpers;

namespace VehicleSmartBooking.Controllers
{
    [Authorize]
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

        private static readonly BookingStatus[] TerminalStatuses =
{
    BookingStatus.Cancelled,
    BookingStatus.Rejected,
    BookingStatus.Completed,
    BookingStatus.Rated
};

        public IActionResult Create()
        {
            ViewData["ActiveNav"] = "MyRequest";
            return View();
        }

        // MyBookings: list view for "My Task"
        [Authorize]
        public async Task<IActionResult> MyBookings()
        {
            ViewData["ActiveNav"] = "MyTask";

            // resolve current user
            var userCodeClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userCodeClaim))
            {
                // if not authenticated, return empty list (or redirect to login if you prefer)
                return View(new List<Booking>());
            }

            var currentUser = await _db.Users.SingleOrDefaultAsync(u => u.UserCode == userCodeClaim && u.IsActive);
            if (currentUser is null)
            {
                return View(new List<Booking>());
            }

            var userId = currentUser.UserId;

            // Query bookings relevant to the current user:
            // - requester
            // - assigned driver
            // - approver (has an approval row referencing this user)
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

            await using var tx = await _db.Database.BeginTransactionAsync();

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

                var assign = await TryAssignFirstAvailableCompanyVehicleAsync(
                    booking.BookingId, vehicleType, startUtc, endUtc);

                if (!assign.Assigned)
                {
                    booking.Status = BookingStatus.WaitingAdminVendorQuotation;
                    booking.IsExternalRental = true;
                }
                else if (tripType == TripType.InProvince)
                {
                    booking.Status = BookingStatus.WaitingDriverAccept;

                    _db.BookingDispatchLogs.Add(new BookingDispatchLog
                    {
                        BookingId = booking.BookingId,
                        AttemptNo = 1,
                        VehicleId = assign.VehicleId!.Value,
                        DriverId = assign.DriverId!.Value,
                        DispatchedAtUtc = now
                    });
                }
                else
                {
                    booking.Status = BookingStatus.WaitingApproval;
                    await CreateApprovalsFromLineManagerChainAsync(booking.BookingId, requester.UserId);
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

            var requester = await _db.Users.AsNoTracking()
                .SingleAsync(u => u.UserId == requesterUserId);

            int? dmId = requester.LineManagerId;
            int? vpId = null;
            int? dmdId = null;

            if (dmId.HasValue)
            {
                var dm = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == dmId.Value);
                vpId = dm?.LineManagerId;

                if (vpId.HasValue)
                {
                    var vp = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == vpId.Value);
                    dmdId = vp?.LineManagerId;
                }
            }

            if (!dmId.HasValue || !vpId.HasValue || !dmdId.HasValue)
            {
                var booking = await _db.Bookings.SingleAsync(b => b.BookingId == bookingId);
                booking.Status = BookingStatus.AdminActionRequired;
                return;
            }

            _db.BookingApprovals.AddRange(
                new BookingApproval
                {
                    BookingId = bookingId,
                    ApproverUserId = dmId.Value,
                    LevelNo = 1,
                    Status = ApprovalStatus.Pending,
                    CreatedAtUtc = now
                },
                new BookingApproval
                {
                    BookingId = bookingId,
                    ApproverUserId = vpId.Value,
                    LevelNo = 2,
                    Status = ApprovalStatus.Pending,
                    CreatedAtUtc = now
                },
                new BookingApproval
                {
                    BookingId = bookingId,
                    ApproverUserId = dmdId.Value,
                    LevelNo = 3,
                    Status = ApprovalStatus.Pending,
                    CreatedAtUtc = now
                }
            );

            await _db.SaveChangesAsync();
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

            // 2) busy: มี booking ทับเวลา + ยังไม่ terminal
            var busy = await _db.Bookings.AsNoTracking()
                .Where(b =>
                    b.AssignedVehicleId != null &&
                    b.AssignedDriverId != null &&
                    !BookingStatusHelper.IsTerminalStatus(b.Status) &&
                    b.StartAtUtc < endUtc &&
                    startUtc < b.EndAtUtc)
                .Select(b => new { b.AssignedVehicleId, b.AssignedDriverId })
                .ToListAsync();

            var busyVehicleIds = busy.Select(x => x.AssignedVehicleId!.Value).ToHashSet();
            var busyDriverIds = busy.Select(x => x.AssignedDriverId!.Value).ToHashSet();

            var pick = candidates.FirstOrDefault(x =>
                !busyVehicleIds.Contains(x.VehicleId) &&
                !busyDriverIds.Contains(x.DriverId));

            if (pick == null)
                return (false, null, null);

            // 3) update booking
            var booking = await _db.Bookings.SingleAsync(b => b.BookingId == bookingId);
            booking.AssignedVehicleId = pick.VehicleId;
            booking.AssignedDriverId = pick.DriverId;
            booking.IsExternalRental = false;

            // 4) update driver load
            var driver = await _db.Drivers.SingleAsync(d => d.DriverId == pick.DriverId);
            driver.LastAssignedAtUtc = DateTime.UtcNow;

            return (true, pick.VehicleId, pick.DriverId);
        }
    }
}
