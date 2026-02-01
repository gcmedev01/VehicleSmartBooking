using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Enums;

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

        public IActionResult Create()
        {
            ViewData["ActiveNav"] = "MyRequest";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] BookingFormModel model, List<IFormFile>? Attachments)
        {
            ViewData["ActiveNav"] = "MyRequest";

            // basic server-side validation
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // parse trip type (hidden BookingMode: "in" | "out")
            var tripType = model.BookingMode?.ToLowerInvariant() == "out" ? TripType.OutProvince : TripType.InProvince;

            // map vehicle type from radio values: pickup/van/sedan
            VehicleType vehicleTypeRequested = VehicleType.Sedan;
            switch ((model.VehicleType ?? "").ToLowerInvariant())
            {
                case "pickup": vehicleTypeRequested = VehicleType.Pickup; break;
                case "van": vehicleTypeRequested = VehicleType.Van; break;
                default: vehicleTypeRequested = VehicleType.Sedan; break;
            }

            // parse datetimes and convert to UTC
            if (!DateTime.TryParse(model.StartAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var startLocal) ||
                !DateTime.TryParse(model.EndAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var endLocal))
            {
                ModelState.AddModelError(string.Empty, "Invalid start or end date/time.");
                return View(model);
            }

            var startUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime();
            var endUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime();

            if (endUtc <= startUtc)
            {
                ModelState.AddModelError(string.Empty, "End time must be after start time.");
                return View(model);
            }

            // determine requester user (prefer authenticated user)
            User? requester = null;
            var userCodeClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(userCodeClaim))
            {
                requester = await _db.Users.SingleOrDefaultAsync(u => u.UserCode == userCodeClaim && u.IsActive);
            }

            // fallback: use EmpCode from form (less secure)
            if (requester is null && !string.IsNullOrWhiteSpace(model.EmpCode))
            {
                requester = await _db.Users.SingleOrDefaultAsync(u => u.UserCode == model.EmpCode && u.IsActive);
            }

            if (requester is null)
            {
                _logger.LogWarning("Booking create failed: requester not found. EmpCode={EmpCode} Claim={Claim}", model.EmpCode, userCodeClaim);
                return Forbid();
            }

            // create booking entity
            var booking = new Booking
            {
                RequesterUserId = requester.UserId,
                TripType = tripType,
                VehicleTypeRequested = vehicleTypeRequested,
                StartAtUtc = startUtc,
                EndAtUtc = endUtc,
                PickupLocation = model.PickupPoint ?? "",
                DestinationLocation = model.Destination ?? "",
                Purpose = string.IsNullOrWhiteSpace(model.Purpose) ? null : model.Purpose,
                PassengerCount = model.PassengerCount,
                DetailNote = string.IsNullOrWhiteSpace(model.OtherDetail) ? null : model.OtherDetail,
                JobNo = string.IsNullOrWhiteSpace(model.RefNo) ? null : model.RefNo,
                CostCenter = string.IsNullOrWhiteSpace(model.CostCenter) ? null : model.CostCenter,
                SONo = string.IsNullOrWhiteSpace(model.SONo) ? null : model.SONo,
                Status = BookingStatus.Submitted
            };

            _db.Bookings.Add(booking);
            await _db.SaveChangesAsync(); // persist to obtain BookingId

            // handle attachments (save to wwwroot/uploads/bookings/{bookingId}/...)
            if (Attachments?.Count > 0)
            {
                var uploadsRoot = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "bookings", booking.BookingId.ToString());
                Directory.CreateDirectory(uploadsRoot);

                foreach (var file in Attachments)
                {
                    if (file == null || file.Length == 0) continue;

                    var safeFileName = Path.GetFileName(file.FileName);
                    var destPath = Path.Combine(uploadsRoot, safeFileName);

                    // avoid overwriting: if exists add numeric suffix
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

                    var storagePath = $"/uploads/bookings/{booking.BookingId}/{Path.GetFileName(finalPath)}";

                    var attachment = new BookingAttachment
                    {
                        BookingId = booking.BookingId,
                        FileName = Path.GetFileName(finalPath),
                        ContentType = file.ContentType,
                        StoragePath = storagePath,
                        UploadedByUserId = requester.UserId
                    };

                    _db.BookingAttachments.Add(attachment);
                }

                await _db.SaveChangesAsync();
            }

            _logger.LogInformation("Booking {BookingId} created by user {UserCode}", booking.BookingId, requester.UserCode);

            // redirect to detail page (implement Detail action to show by id) or MyBookings
            return RedirectToAction(nameof(Detail), new { id = booking.BookingId });
        }

        // MyBookings: list view for "My Task" — now returns all bookings requested by the current user
        [Authorize]
        public async Task<IActionResult> MyBookings()
        {
            ViewData["ActiveNav"] = "MyTask";

            // resolve current user
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

            // Show bookings where current user is the requester (explicit requirement)
            var bookings = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.Requester)
                .Where(b => b.RequesterUserId == userId)
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

            ViewData["ActiveNav"] = "MyTask";
            return View(booking);
        }

        /*// POST: /Booking/Cancel/{id}
        // requester ยกเลิกก่อน terminal
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(long id)
        {
            
        }

        // POST: /Booking/Rate/{id}
        // requester ให้คะแนน driver
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rate(long id, int score, string? comment)
        {

        }

        // POST: /Booking/VendorDecision/{id}
        // Accept / Reject vendor (Flow 3)
        public async Task<IActionResult> VendorDecision(long id, string decision)
        {

        }*/
    }
}