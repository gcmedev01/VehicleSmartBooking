using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Enums;
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
        private readonly IWebHostEnvironment _env;

        private static readonly HashSet<string> AllowedImageExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".heic" };

        public DriverController(
            VehicleBookingDbContext db,
            ICurrentUserService currentUser,
            IDriverWorkflowService driverWorkflow,
            IEmailNotificationService emailNotifications,
            ILogger<DriverController> logger,
            IWebHostEnvironment env)
        {
            _db = db;
            _currentUser = currentUser;
            _driverWorkflow = driverWorkflow;
            _emailNotifications = emailNotifications;
            _logger = logger;
            _env = env;
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
                .Include(b => b.CompletionPhotos)
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
        public async Task<IActionResult> Complete(
            long id,
            List<IFormFile>? PhotoOdometer,
            List<IFormFile>? PhotoExterior,
            List<IFormFile>? PhotoInterior)
        {
            ViewData["ActiveNav"] = "DriverJobs";

            var driver = await _currentUser.GetCurrentDriverAsync(User);
            if (driver is null) return Forbid();

            // Pre-check: verify assignment and completable status before touching files
            var bookingCheck = await _db.Bookings
                .AsNoTracking()
                .SingleOrDefaultAsync(b => b.BookingId == id);

            if (bookingCheck is null) return NotFound();

            if (bookingCheck.AssignedDriverId != driver.DriverId)
            {
                TempData["Error"] = "คุณไม่ได้รับมอบหมายงานนี้";
                return RedirectToAction(nameof(Detail), new { id });
            }

            if (!TripFlowHelper.CanDriverComplete(bookingCheck))
            {
                TempData["Error"] = "งานนี้ไม่สามารถจบงานได้ในสถานะปัจจุบัน";
                return RedirectToAction(nameof(Detail), new { id });
            }

            // Validate all 3 required photo inputs (at least 1 file each)
            var (ok1, err1) = ValidateCompletionPhotos(PhotoOdometer, "รูปเลขกิโลเมตรก่อนเริ่ม/หลังให้บริการ");
            var (ok2, err2) = ValidateCompletionPhotos(PhotoExterior, "รูปสภาพรถและภายนอกรถ");
            var (ok3, err3) = ValidateCompletionPhotos(PhotoInterior, "รูปภาพตรวจสอบสภาพรถภายใน");

            if (!ok1 || !ok2 || !ok3)
            {
                TempData["Error"] = string.Join(" | ", new[] { err1, err2, err3 }.Where(e => e != null));
                return RedirectToAction(nameof(Detail), new { id });
            }

            // Save files to disk
            var uploadDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "driver-completion-photos", id.ToString());
            Directory.CreateDirectory(uploadDir);

            var now = DateTime.UtcNow;
            var groupedFiles = new[]
            {
                (Files: PhotoOdometer!, Group: DriverCompletionPhotoGroup.OdometerBeforeAfter),
                (Files: PhotoExterior!,  Group: DriverCompletionPhotoGroup.ExteriorCondition),
                (Files: PhotoInterior!,  Group: DriverCompletionPhotoGroup.InteriorCondition),
            };

            foreach (var entry in groupedFiles)
            {
                foreach (var file in entry.Files)
                {
                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    var storedName = $"{Guid.NewGuid():N}{ext}";
                    var diskPath = Path.Combine(uploadDir, storedName);

                    await using (var stream = System.IO.File.Create(diskPath))
                    {
                        await file.CopyToAsync(stream);
                    }

                    _db.DriverCompletionPhotos.Add(new DriverCompletionPhoto
                    {
                        BookingId = id,
                        PhotoGroup = entry.Group,
                        OriginalFileName = Path.GetFileName(file.FileName),
                        StoredFileName = storedName,
                        RelativePath = $"/uploads/driver-completion-photos/{id}/{storedName}",
                        ContentType = file.ContentType,
                        FileSizeBytes = file.Length,
                        UploadedByUserId = driver.UserId,
                        UploadedAtUtc = now
                    });
                }
            }

            // CompleteAsync validates status again and calls SaveChangesAsync,
            // which persists both the booking status change and the queued photo records.
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

        private static (bool Ok, string? Error) ValidateCompletionPhotos(List<IFormFile>? files, string label)
        {
            if (files == null || files.Count == 0)
                return (false, $"กรุณาแนบ{label}");

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file.FileName);
                if (!AllowedImageExtensions.Contains(ext))
                    return (false, $"{label}: รองรับเฉพาะไฟล์ .jpg .jpeg .png .webp .heic");

                if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    return (false, $"{label}: ไม่ใช่ไฟล์รูปภาพ");
            }

            return (true, null);
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
