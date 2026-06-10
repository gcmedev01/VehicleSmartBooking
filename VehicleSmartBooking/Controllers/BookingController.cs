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
using VehicleBooking.Web.Domain.Services;
using VehicleSmartBooking.Models;

namespace VehicleSmartBooking.Controllers
{
    [Authorize(Roles = "User,Approver,Admin")]
    public class BookingController : Controller
    {
        private readonly VehicleBookingDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<BookingController> _logger;
        private readonly IEmailNotificationService _emailNotifications;
        private readonly ApprovalChainBuilder _approvalChainBuilder;

        public BookingController(VehicleBookingDbContext db, IWebHostEnvironment env, ILogger<BookingController> logger, IEmailNotificationService emailNotifications, ApprovalChainBuilder approvalChainBuilder)
        {
            _db = db;
            _env = env;
            _logger = logger;
            _emailNotifications = emailNotifications;
            _approvalChainBuilder = approvalChainBuilder;
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
            public string? ServiceOption { get; set; } // "external" | "personal"

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
            public string? SpecialOccasionType { get; set; }
            public string? SpecialOccasionRemark { get; set; }
            public string? CostCenter { get; set; }
            public string? SONo { get; set; }
        }

        public IActionResult Create()
        {
            ViewData["ActiveNav"] = "MyRequest";

            PopulateCreateViewDataAsync().GetAwaiter().GetResult();

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
                .Include(b => b.Rating)
                // include assigned driver and vehicle to show details in view
                .Include(b => b.AssignedDriver)
                    .ThenInclude(d => d.User)
                .Include(b => b.AssignedVehicle)
                .Include(b => b.CompletionPhotos)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking is null) return NotFound();

            var userCode = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isRequester = !string.IsNullOrWhiteSpace(userCode) &&
                              string.Equals(booking.Requester?.UserCode, userCode, StringComparison.OrdinalIgnoreCase);

            // mark layout active nav for Detail -> MyTask
            ViewData["ActiveNav"] = "MyTask";

            return View(booking);
        }

        [Authorize]
        [HttpGet("/booking/rate/{id:long}")]
        public async Task<IActionResult> Rate(long id)
        {
            var booking = await _db.Bookings
                .Include(b => b.Requester)
                .Include(b => b.AssignedDriver)
                    .ThenInclude(d => d.User)
                .Include(b => b.AssignedVehicle)
                .Include(b => b.Rating)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking is null) return NotFound();

            var userCode = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isRequester = !string.IsNullOrWhiteSpace(userCode) &&
                              string.Equals(booking.Requester?.UserCode, userCode, StringComparison.OrdinalIgnoreCase);

            if (!isRequester || booking.Status != BookingStatus.Completed || booking.IsPersonal || !booking.AssignedDriverId.HasValue || booking.Rating != null)
            {
                return RedirectToAction(nameof(Detail), new { id });
            }

            ViewData["ActiveNav"] = "MyTask";

            return View(new BookingRatingViewModel { Booking = booking });
        }

        [Authorize]
        [HttpPost("/booking/rate/{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rate(long id, [FromForm] BookingRatingViewModel model)
        {
            var booking = await _db.Bookings
                .Include(b => b.Requester)
                .Include(b => b.AssignedDriver)
                .Include(b => b.AssignedVehicle)
                .Include(b => b.Rating)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking is null) return NotFound();

            var userCode = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isRequester = !string.IsNullOrWhiteSpace(userCode) &&
                              string.Equals(booking.Requester?.UserCode, userCode, StringComparison.OrdinalIgnoreCase);

            if (!isRequester || booking.Status != BookingStatus.Completed || booking.IsPersonal || !booking.AssignedDriverId.HasValue || booking.Rating != null)
            {
                return RedirectToAction(nameof(Detail), new { id });
            }

            if (!ModelState.IsValid)
            {
                model.Booking = booking;
                ViewData["ActiveNav"] = "MyTask";
                return View(model);
            }

            var now = DateTime.UtcNow;

            var rating = new DriverRating
            {
                BookingId = booking.BookingId,
                DriverId = booking.AssignedDriverId.Value,
                Score1 = model.Score1!.Value,
                Score2 = model.Score2!.Value,
                Score3 = model.Score3!.Value,
                Score4 = model.Score4!.Value,
                Score5 = model.Score5!.Value,
                Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim(),
                CreatedAtUtc = now
            };

            _db.DriverRatings.Add(rating);
            booking.Status = BookingStatus.Rated;
            booking.UpdatedAtUtc = now;

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Detail), new { id });
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
                "electric" => VehicleType.Electric,
                _ => VehicleType.Sedan
            };

            var isElectricVehicle = vehicleType == VehicleType.Electric;
            var usePersonal = string.Equals(model.ServiceOption?.Trim(), "personal", StringComparison.OrdinalIgnoreCase)
                              && tripType == TripType.OutProvince;
            var specialOccasionType = ParseSpecialOccasionType(model.SpecialOccasionType, tripType);

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
                    SpecialOccasionType = specialOccasionType,
                    SpecialOccasionRemark = string.IsNullOrWhiteSpace(model.SpecialOccasionRemark) ? null : model.SpecialOccasionRemark.Trim(),
                    Status = BookingStatus.Submitted,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                _db.Bookings.Add(booking);
                await _db.SaveChangesAsync();

                if (isElectricVehicle)
                {
                    var assignedVehicleId = await TryAssignFirstAvailableVehicleWithoutDriverAsync(
                        booking.BookingId,
                        vehicleType,
                        startUtc,
                        endUtc);

                    if (!assignedVehicleId.HasValue)
                    {
                        await tx.RollbackAsync();
                        ModelState.AddModelError("", "ไม่มีรถไฟฟ้าว่างในช่วงเวลาที่เลือก");
                        await PopulateCreateViewDataAsync();
                        return View(model);
                    }

                    booking.AssignedVehicleId = assignedVehicleId.Value;
                    booking.AssignedDriverId = null;
                    booking.IsExternalRental = false;
                    booking.IsPersonal = false;
                    booking.Status = BookingStatus.Submitted;
                    booking.UpdatedAtUtc = now;

                    await SaveAttachmentsAsync(booking.BookingId, requester.UserId, Attachments);
                    await CreateApprovalsFromLineManagerChainAsync(booking.BookingId, requester.UserId);
                    await _db.SaveChangesAsync();

                    booking = await _db.Bookings
                        .Include(b => b.Requester)
                        .Include(b => b.AssignedVehicle)
                        .Include(b => b.Approvals)
                            .ThenInclude(a => a.Approver)
                        .FirstAsync(b => b.BookingId == booking.BookingId);

                    await tx.CommitAsync();

                    var adminEmailsForElectric = await _db.Users
                        .AsNoTracking()
                        .Where(u => (u.RoleFlags & 2) != 0 && u.Email != null)
                        .Select(u => u.Email!)
                        .ToListAsync();

                    try
                    {
                        await _emailNotifications.NotifyStatusChangedAsync(
                            booking,
                            adminEmailsForElectric,
                            ownerEmail: null,
                            statusChangedAtUtc: booking.UpdatedAtUtc,
                            relativeUrl: $"/Admin/Detail/{booking.BookingId}");

                        await _emailNotifications.NotifyStatusChangedAsync(
                            booking,
                            Array.Empty<string>(),
                            requester.Email,
                            statusChangedAtUtc: booking.UpdatedAtUtc,
                            relativeUrl: $"/Booking/Detail/{booking.BookingId}");

                        if (booking.Status == BookingStatus.WaitingApproval)
                        {
                            var pendingApprovals = booking.Approvals
                                .Where(a => a.Status == ApprovalStatus.Pending && a.Approver?.Email != null)
                                .ToList();

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
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send electric booking email for booking {BookingId}", booking.BookingId);
                    }

                    return RedirectToAction(nameof(Detail), new { id = booking.BookingId });
                }

                if (usePersonal)
                {
                    await SaveAttachmentsAsync(booking.BookingId, requester.UserId, Attachments);

                    booking.Status = BookingStatus.WaitingAdminPersonal;
                    booking.IsPersonal = true;
                    booking.IsExternalRental = false;
                    booking.UpdatedAtUtc = now;

                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();

                    var personalAdminEmails = await _db.Users
                        .AsNoTracking()
                        .Where(u => (u.RoleFlags & 2) != 0 && u.Email != null)
                        .Select(u => u.Email!)
                        .ToListAsync();

                    try
                    {
                        await _emailNotifications.NotifyStatusChangedAsync(
                            booking,
                            personalAdminEmails,
                            ownerEmail: null,
                            statusChangedAtUtc: booking.UpdatedAtUtc,
                            relativeUrl: $"/Admin/Detail/{booking.BookingId}");

                        await _emailNotifications.NotifyActionRequiredAsync(
                            booking,
                            personalAdminEmails,
                            "กรุณาอนุมัติรถส่วนตัว",
                            relativeUrl: $"/Admin/Detail/{booking.BookingId}");

                        await _emailNotifications.NotifyStatusChangedAsync(
                            booking,
                            Array.Empty<string>(),
                            requester.Email,
                            statusChangedAtUtc: booking.UpdatedAtUtc,
                            relativeUrl: $"/Booking/Detail/{booking.BookingId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send personal booking email for booking {BookingId}", booking.BookingId);
                    }

                    return RedirectToAction(nameof(Detail), new { id = booking.BookingId });
                }

                await SaveAttachmentsAsync(booking.BookingId, requester.UserId, Attachments);

                // attempt assignment but do not let assignment errors prevent booking creation
                var assignResult = (Assigned: false, VehicleId: (int?)null, DriverId: (int?)null);
                try
                {
                    assignResult = await TryAssignFirstAvailableCompanyVehicleAsync(
                        booking.BookingId,
                        vehicleType,
                        startUtc,
                        endUtc,
                        tripType == TripType.OutProvince);
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
                    booking.IsExternalRental = true;

                    if (booking.SpecialOccasionType.HasValue)
                    {
                        booking.Status = BookingStatus.Submitted;
                        await CreateApprovalsFromLineManagerChainAsync(booking.BookingId, requester.UserId);
                    }
                    else
                    {
                        booking.Status = BookingStatus.WaitingAdminVendorQuotation;
                    }
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

                    // If this booking is a special occasion, require approvals regardless of in/out province
                    if (booking.SpecialOccasionType.HasValue)
                    {
                        booking.Status = BookingStatus.Submitted;
                        // create approval rows according to approval scenario (SpecialOccasion)
                        await CreateApprovalsFromLineManagerChainAsync(booking.BookingId, requester.UserId);
                    }
                    else if (tripType == TripType.InProvince)
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
                        booking.Status = BookingStatus.Submitted;
                        await CreateApprovalsFromLineManagerChainAsync(booking.BookingId, requester.UserId);
                    }
                    }
                }

                booking.UpdatedAtUtc = now;
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

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
                        requester.Email,
                        statusChangedAtUtc: booking.UpdatedAtUtc,
                        relativeUrl: $"/Booking/Detail/{booking.BookingId}");

                    if (booking.Status == BookingStatus.WaitingApproval)
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
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send booking create email for booking {BookingId}", booking.BookingId);
                }

                return RedirectToAction(nameof(Detail), new { id = booking.BookingId });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        private async Task PopulateCreateViewDataAsync()
        {
            var userCode = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userCode))
            {
                return;
            }

            var requester = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserCode == userCode && u.IsActive);
            if (requester == null)
            {
                return;
            }

            ViewBag.Users = requester;

            var preview = await _approvalChainBuilder.BuildPreviewAsync(requester.UserId);
            ViewBag.ApprovalPreview = preview
                .Select(a => new
                {
                    a.LevelNo,
                    Name = a.User.UsernameTH ?? a.User.UsernameEN ?? a.User.UserCode,
                    Position = a.User.PositionEN ?? ""
                })
                .ToList();

            ViewBag.ApprovalPreviewNote = preview.Count == 0
                ? "เงื่อนไขการอนุมัติขึ้นอยู่กับประเภทคำขอและสายบังคับบัญชา"
                : null;
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

            var bookingForApproval = await _db.Bookings
                .SingleAsync(b => b.BookingId == bookingId);

            var scenario = DetermineApprovalScenario(bookingForApproval);
            var chain = await _approvalChainBuilder.BuildAsync(requesterUserId, scenario);
            if (chain.IsAutoApproved)
            {
                bookingForApproval.Status = GetAutoApprovedStatus(bookingForApproval);
                bookingForApproval.UpdatedAtUtc = now;
                await _db.SaveChangesAsync();
                return;
            }

            if (chain.IsAdminActionRequired || chain.Approvers.Count == 0)
            {
                bookingForApproval.Status = BookingStatus.AdminActionRequired;
                bookingForApproval.UpdatedAtUtc = now;
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

        private static ApprovalScenario DetermineApprovalScenario(Booking booking)
        {
            if (booking.SpecialOccasionType.HasValue)
            {
                return ApprovalScenario.SpecialOccasion;
            }

            if (booking.IsPersonal)
            {
                return ApprovalScenario.PersonalVehicle;
            }

            if (booking.VehicleTypeRequested == VehicleType.Electric)
            {
                var startDate = booking.StartAtUtc.ToLocalTime().Date;
                var endDate = booking.EndAtUtc.ToLocalTime().Date;
                return startDate == endDate
                    ? ApprovalScenario.ElectricSingleDay
                    : ApprovalScenario.ElectricMultiDay;
            }

            if (booking.TripType == TripType.OutProvince)
            {
                return ApprovalScenario.OutProvinceGeneral;
            }

            return ApprovalScenario.None;
        }

        private static BookingStatus GetAutoApprovedStatus(Booking booking)
        {
            if (booking.VehicleTypeRequested == VehicleType.Electric)
            {
                return BookingStatus.ApprovedSelfDrive;
            }

            if (booking.IsPersonal)
            {
                return BookingStatus.Completed;
            }

            if (booking.IsExternalRental)
            {
                return booking.SpecialOccasionType.HasValue
                    ? BookingStatus.WaitingAdminVendorQuotation
                    : BookingStatus.WaitingAdminVendorConfirm;
            }

            return BookingStatus.WaitingDriverAccept;
        }

        private static SpecialOccasionType? ParseSpecialOccasionType(string? value, TripType tripType)
        {
            // Accept special occasion value regardless of trip type. Server-side UI already restricts
            // selection to vehicle type == van, but enforce here as a pure parser: if value empty -> null.
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim().ToLowerInvariant() switch
            {
                "wedding" => SpecialOccasionType.Wedding,
                "ordination" => SpecialOccasionType.Ordination,
                _ => null
            };
        }

        [Authorize]
        [HttpGet("/booking/preview-approvals")]
        public async Task<IActionResult> PreviewApprovals([FromQuery] string? bookingMode, [FromQuery] string? vehicleType, [FromQuery] string? specialOccasionType)
        {
            var userCode = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userCode)) return Json(new { ok = false, message = "Unauthenticated" });

            var requester = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserCode == userCode && u.IsActive);
            if (requester == null) return Json(new { ok = false, message = "Requester not found" });

            var tripType = (bookingMode ?? "").ToLowerInvariant() == "out" ? TripType.OutProvince : TripType.InProvince;
            var vType = (vehicleType ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "pickup" => VehicleType.Pickup,
                "van" => VehicleType.Van,
                "sedan" => VehicleType.Sedan,
                "electric" => VehicleType.Electric,
                _ => VehicleType.Sedan
            };

            var special = ParseSpecialOccasionType(specialOccasionType, tripType);

            var bookingStub = new Booking
            {
                TripType = tripType,
                VehicleTypeRequested = vType,
                SpecialOccasionType = special,
                IsPersonal = false
            };

            var scenario = DetermineApprovalScenario(bookingStub);
            var chain = await _approvalChainBuilder.BuildAsync(requester.UserId, scenario);

            var approvers = chain.Approvers.Select(a => new
            {
                LevelNo = a.LevelNo,
                Name = a.User.UsernameTH ?? a.User.UsernameEN ?? a.User.UserCode,
                Position = a.User.PositionEN ?? string.Empty
            }).ToList();

            return Json(new { ok = true, approvers, isAuto = chain.IsAutoApproved, isAdminActionRequired = chain.IsAdminActionRequired });
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
        public async Task<IActionResult> CheckAvailability([FromQuery] string? vehicleType, [FromQuery] string? startAt, [FromQuery] string? endAt, [FromQuery] string? bookingMode)
        {
            var type = (vehicleType ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "pickup" => VehicleType.Pickup,
                "van" => VehicleType.Van,
                "sedan" => VehicleType.Sedan,
                "electric" => VehicleType.Electric,
                _ => (VehicleType?)null
            };

            if (!type.HasValue)
            {
                return Ok(new { ok = false, message = "กรุณาเลือกประเถทรถ" });
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
            var isOutProvince = string.Equals(bookingMode, "out", StringComparison.OrdinalIgnoreCase);

            if (type == VehicleType.Electric)
            {
                var electricVehicleIds = await _db.Vehicles
                    .AsNoTracking()
                    .Where(v => v.IsActive && v.VehicleType == VehicleType.Electric)
                    .Select(v => v.VehicleId)
                    .ToListAsync();

                if (electricVehicleIds.Count == 0)
                {
                    return Ok(new
                    {
                        ok = true,
                        total = 0,
                        available = 0,
                        message = "ไม่พบรถไฟฟ้าในระบบ"
                    });
                }

                var electricTerminalStatuses = BookingStatusHelper.TerminalStatuses;
                var busyVehicleIds = await _db.Bookings
                    .AsNoTracking()
                    .Where(b =>
                        !electricTerminalStatuses.Contains(b.Status) &&
                        !b.IsExternalRental &&
                        b.AssignedVehicleId != null &&
                        electricVehicleIds.Contains(b.AssignedVehicleId.Value) &&
                        b.StartAtUtc < endUtc &&
                        startUtc < b.EndAtUtc)
                    .Select(b => b.AssignedVehicleId!.Value)
                    .Distinct()
                    .ToListAsync();

                var electricPendingUnassignedCount = await _db.Bookings
                    .AsNoTracking()
                    .Where(b =>
                        !electricTerminalStatuses.Contains(b.Status) &&
                        !b.IsExternalRental &&
                        b.AssignedVehicleId == null &&
                        b.AssignedDriverId == null &&
                        b.VehicleTypeRequested == VehicleType.Electric &&
                        b.StartAtUtc < endUtc &&
                        startUtc < b.EndAtUtc)
                    .CountAsync();

                var electricAvailable = electricVehicleIds.Count - busyVehicleIds.Count - electricPendingUnassignedCount;

                return Ok(new
                {
                    ok = true,
                    total = electricVehicleIds.Count,
                    available = Math.Max(0, electricAvailable)
                });
            }

            var candidates = await (
                from d in _db.Drivers.AsNoTracking()
                join v in _db.Vehicles.AsNoTracking() on d.VehicleId equals v.VehicleId
                where d.IsActive
                      && (!isOutProvince || d.CanDriveOutOfProvince)
                      && v.IsActive
                      && v.VehicleType == type.Value
                select new { d.DriverId, d.VehicleId }
            ).ToListAsync();

            if (candidates.Count == 0)
            {
                return Ok(new
                {
                    ok = true,
                    total = 0,
                    available = 0,
                    message = isOutProvince
                        ? "ไม่พบรถพร้อมพนักงานขับรถที่สามารถขับออกนอกจังหวัดได้"
                        : "ไม่พบรถบริษัทในประเภทรถนี้"
                });
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

        private async Task<(bool Assigned, int? VehicleId, int? DriverId)> TryAssignFirstAvailableCompanyVehicleAsync(long bookingId, VehicleType vehicleType, DateTime startUtc, DateTime endUtc, bool requireOutProvinceDriver = false)
        {
            // 1) candidate: driver active + vehicle active + type ตรง
            var candidates = await (
                from d in _db.Drivers.AsNoTracking()
                join v in _db.Vehicles.AsNoTracking() on d.VehicleId equals v.VehicleId
                where d.IsActive
                      && (!requireOutProvinceDriver || d.CanDriveOutOfProvince)
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

        private async Task<int?> TryAssignFirstAvailableVehicleWithoutDriverAsync(long bookingId, VehicleType vehicleType, DateTime startUtc, DateTime endUtc)
        {
            var candidates = await _db.Vehicles
                .AsNoTracking()
                .Where(v => v.IsActive && v.VehicleType == vehicleType)
                .OrderBy(v => v.PlateNo)
                .Select(v => v.VehicleId)
                .ToListAsync();

            if (candidates.Count == 0)
            {
                return null;
            }

            var terminal = BookingStatusHelper.TerminalStatuses;
            var availableVehicleIds = new List<int>();

            foreach (var vehicleId in candidates)
            {
                var isBusy = await _db.Bookings.AsNoTracking().AnyAsync(b =>
                    b.BookingId != bookingId &&
                    !terminal.Contains(b.Status) &&
                    b.AssignedVehicleId == vehicleId &&
                    b.StartAtUtc < endUtc &&
                    startUtc < b.EndAtUtc);

                if (!isBusy)
                {
                    availableVehicleIds.Add(vehicleId);
                }
            }

            if (availableVehicleIds.Count == 0)
            {
                return null;
            }

            var pendingUnassignedCount = await _db.Bookings.AsNoTracking().CountAsync(b =>
                b.BookingId != bookingId &&
                !terminal.Contains(b.Status) &&
                !b.IsExternalRental &&
                b.AssignedVehicleId == null &&
                b.AssignedDriverId == null &&
                b.VehicleTypeRequested == vehicleType &&
                b.StartAtUtc < endUtc &&
                startUtc < b.EndAtUtc);

            if (availableVehicleIds.Count <= pendingUnassignedCount)
            {
                return null;
            }

            return availableVehicleIds[pendingUnassignedCount];
        }

        [Authorize]
        [HttpPost("/booking/accept-vendor/{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptVendor(long id)
        {
            var booking = await _db.Bookings
                .Include(b => b.ExternalRental)
                .Include(b => b.Requester)
                .SingleOrDefaultAsync(b => b.BookingId == id);
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
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send vendor accept email for booking {BookingId}", booking.BookingId);
            }

            TempData["Success"] = "You accepted the vendor quote. The request will go through approval process.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        [Authorize]
        [HttpPost("/booking/reject-vendor/{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectVendor(long id, string? reason)
        {
            var booking = await _db.Bookings
                .Include(b => b.ExternalRental)
                .Include(b => b.Requester)
                .SingleOrDefaultAsync(b => b.BookingId == id);
            if (booking == null) return NotFound();

            // only requester can reject vendor
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
                _logger.LogWarning(ex, "Failed to send vendor reject email for booking {BookingId}", booking.BookingId);
            }

            TempData["Success"] = "You rejected the vendor quote.";
            return RedirectToAction(nameof(Detail), new { id });
        }
    }
}
