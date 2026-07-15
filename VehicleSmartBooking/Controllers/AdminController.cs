using ClosedXML.Excel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Enums;
using VehicleBooking.Web.Domain.Helpers;
using VehicleBooking.Web.Domain.Services;
using static VehicleSmartBooking.Controllers.BookingController;

namespace VehicleSmartBooking.Controllers
{

    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly VehicleBookingDbContext _db;
        private readonly ILogger<AdminController> _logger;
        private readonly IPasswordHasher _hasher;
        private readonly IEmailNotificationService _emailNotifications;
        private readonly ApprovalChainBuilder _approvalChainBuilder;
        private readonly IDriverBookingNotificationService _driverBookingNotifications;

        private const int ROLE_USER = 1;
        private const int ROLE_DRIVER = 4;
        private const int ROLE_ADMIN = 2;
        private const int ROLE_APPROVER = 8;

        public AdminController(VehicleBookingDbContext db, ILogger<AdminController> logger, IPasswordHasher hasher, IEmailNotificationService emailNotifications, ApprovalChainBuilder approvalChainBuilder, IDriverBookingNotificationService driverBookingNotifications)
        {
            _db = db;
            _logger = logger;
            _hasher = hasher;
            _emailNotifications = emailNotifications;
            _approvalChainBuilder = approvalChainBuilder;
            _driverBookingNotifications = driverBookingNotifications;
        }

        // GET: /Admin/Worklist
        [HttpGet]
        public async Task<IActionResult> Worklist()
        {
            ViewData["ActiveNav"] = "AdminWorklist";

            var statuses = new[] { BookingStatus.WaitingAdminVendorQuotation, BookingStatus.WaitingAdminVendorConfirm, BookingStatus.WaitingAdminPersonal, BookingStatus.AdminActionRequired };

            var items = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.Requester)
                .Where(b => statuses.Contains(b.Status))
                .OrderByDescending(b => b.StartAtUtc)
                .ToListAsync();

            return View(items);
        }

        // GET: /Admin/WorklistCount (JSON count for the sidebar nav badge)
        [HttpGet]
        public async Task<IActionResult> WorklistCount()
        {
            var statuses = new[] { BookingStatus.WaitingAdminVendorQuotation, BookingStatus.WaitingAdminVendorConfirm, BookingStatus.WaitingAdminPersonal, BookingStatus.AdminActionRequired };

            var count = await _db.Bookings
                .AsNoTracking()
                .CountAsync(b => statuses.Contains(b.Status));

            return Json(new { count });
        }

        [Authorize]
        public async Task<IActionResult> AllBookings(
            string? search,
            BookingStatus? status,
            TripType? tripType,
            string? serviceType,
            DateTime? startDate,
            DateTime? endDate,
            string? functionAbbr,
            string? deptAbbr,
            string? divAbbr,
            int page = 1,
            int pageSize = 20)
        {
            ViewData["ActiveNav"] = "AllTasks";

            if (!Request.Query.ContainsKey("startDate"))
            {
                startDate = DateTime.Today;
            }

            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

            var trimmedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var trimmedServiceType = string.IsNullOrWhiteSpace(serviceType) ? null : serviceType.Trim().ToLowerInvariant();

            ViewBag.Search = trimmedSearch;
            ViewBag.Status = status;
            ViewBag.TripType = tripType;
            ViewBag.ServiceType = trimmedServiceType;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.FunctionAbbr = functionAbbr?.Trim();
            ViewBag.DeptAbbr = deptAbbr?.Trim();
            ViewBag.DivAbbr = divAbbr?.Trim();

            await PopulateBookingFilterOptionsAsync();

            void SetPagingViewBag(int currentPage, int totalCount, int totalPages)
            {
                ViewBag.Page = currentPage;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalCount = totalCount;
                ViewBag.TotalPages = totalPages;
            }

            if (startDate.HasValue && endDate.HasValue && endDate.Value.Date < startDate.Value.Date)
            {
                TempData["Error"] = "วันที่สิ้นสุดต้องมากกว่าหรือเท่ากับวันที่เริ่มต้น";
                SetPagingViewBag(1, 0, 0);
                return View(new List<AllBookingsRowVm>());
            }

            var userCodeClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userCodeClaim))
            {
                SetPagingViewBag(1, 0, 0);
                return View(new List<AllBookingsRowVm>());
            }

            var query = BuildBookingsFilterQuery(
                trimmedSearch, status, tripType, trimmedServiceType,
                functionAbbr, deptAbbr, divAbbr, startDate, endDate);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages > 0 && page > totalPages) page = totalPages;

            var rawRows = await query
                .OrderBy(b => b.StartAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new
                {
                    b.BookingId,
                    b.JobNo,
                    b.TripType,
                    b.VehicleTypeRequested,
                    b.Status,
                    b.IsPersonal,
                    b.IsExternalRental,
                    b.PickupLocation,
                    b.DestinationLocation,
                    b.StartAtUtc,
                    b.EndAtUtc,
                    RequesterName = b.Requester.UsernameTH,
                    InternalPlateNo = b.AssignedVehicle != null ? b.AssignedVehicle.PlateNo : null,
                    RentalPlateNo = b.ExternalRental != null ? b.ExternalRental.RentalPlateNo : null,
                    VendorName = b.ExternalRental != null ? b.ExternalRental.VendorName : null
                })
                .ToListAsync();

            var bookings = rawRows.Select(b => new AllBookingsRowVm
            {
                BookingId = b.BookingId,
                JobNo = b.JobNo,
                TripType = b.TripType,
                VehicleTypeRequested = b.VehicleTypeRequested,
                Status = b.Status,
                RequesterName = b.RequesterName,
                PlateNo = b.IsExternalRental ? b.RentalPlateNo : b.InternalPlateNo,
                ServiceTypeText = GetServiceTypeText(b.IsPersonal, b.IsExternalRental),
                ServiceTypePlateNo = (b.IsExternalRental && !string.IsNullOrWhiteSpace(b.RentalPlateNo)) ? b.RentalPlateNo : null,
                PickupDisplay = Truncate(b.PickupLocation, 50),
                DestinationDisplay = Truncate(b.DestinationLocation, 50),
                StartDisplay = b.StartAtUtc.ToLocalTime().ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture),
                EndDisplay = b.EndAtUtc.ToLocalTime().ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture),
                VendorName = (b.IsExternalRental && !string.IsNullOrWhiteSpace(b.VendorName)) ? Truncate(b.VendorName, 50) : null,
            }).ToList();

            SetPagingViewBag(page, totalCount, totalPages);

            return View(bookings);
        }

        public sealed class AllBookingsRowVm
        {
            public long BookingId { get; set; }
            public string? JobNo { get; set; }
            public TripType TripType { get; set; }
            public VehicleType VehicleTypeRequested { get; set; }
            public BookingStatus Status { get; set; }
            public string? RequesterName { get; set; }

            // Plate shown under "ประเภทรถ" — internal vehicle plate for company cars, otherwise the
            // external rental's plate (once one has been recorded).
            public string? PlateNo { get; set; }

            public string ServiceTypeText { get; set; } = "";
            // Populated only for external-rental rows that already have a rental plate on file.
            public string? ServiceTypePlateNo { get; set; }

            public string PickupDisplay { get; set; } = "";
            public string DestinationDisplay { get; set; } = "";

            public string StartDisplay { get; set; } = "";
            public string EndDisplay { get; set; } = "";
            public string? VendorName { get; set; } = "";
        }

        [HttpGet]
        public async Task<IActionResult> ExportBookingsExcel(
            string? search,
            BookingStatus? status,
            TripType? tripType,
            string? serviceType,
            DateTime? startDate,
            DateTime? endDate,
            string? functionAbbr,
            string? deptAbbr,
            string? divAbbr)
        {
            ViewData["ActiveNav"] = "AllTasks";

            if (startDate.HasValue && endDate.HasValue && endDate.Value.Date < startDate.Value.Date)
            {
                TempData["Error"] = "วันที่สิ้นสุดต้องมากกว่าหรือเท่ากับวันที่เริ่มต้น";
                return RedirectToAction(nameof(AllBookings), new { search, status, tripType, serviceType, startDate, endDate, functionAbbr, deptAbbr, divAbbr });
            }

            var trimmedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var trimmedServiceType = string.IsNullOrWhiteSpace(serviceType) ? null : serviceType.Trim().ToLowerInvariant();

            var query = BuildBookingsFilterQuery(
                    trimmedSearch, status, tripType, trimmedServiceType,
                    functionAbbr, deptAbbr, divAbbr, startDate, endDate)
                .Include(b => b.AssignedVehicle)
                .Include(b => b.AssignedDriver)
                    .ThenInclude(d => d.User)
                .Include(b => b.ExternalRental);

            var bookings = await query
                .OrderByDescending(b => b.StartAtUtc)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Bookings Report");

            var headers = new[]
            {
                "Booking #",
                "วันที่เริ่มต้น",
                "วันที่สิ้นสุด",
                "ผู้ขอรถ",
                "รหัสพนักงาน",
                "ประเภทการเดินทาง",
                "ประเภทรถ",
                "ประเภทบริการ",
                "สถานะ",
                "จุดรับ",
                "ปลายทาง",
                "วัตถุประสงค์",
                "จำนวนผู้โดยสาร",
                "เลขอ้างอิง",
                "Cost Center",
                "SO No",
                "ทะเบียนรถ",
                "พนักงานขับรถ",
                "Vendor",
                "Vendor Price",
                "วันที่สร้างรายการ"
            };

            for (var i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
            }

            var headerRange = worksheet.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9EAF7");

            for (var index = 0; index < bookings.Count; index++)
            {
                var booking = bookings[index];
                var row = index + 2;

                worksheet.Cell(row, 1).Value = $"VS-{booking.BookingId}";
                worksheet.Cell(row, 2).Value = booking.StartAtUtc.ToLocalTime();
                worksheet.Cell(row, 3).Value = booking.EndAtUtc.ToLocalTime();
                worksheet.Cell(row, 4).Value = booking.Requester?.UsernameTH ?? booking.Requester?.UsernameEN ?? string.Empty;
                worksheet.Cell(row, 5).Value = booking.Requester?.UserCode ?? string.Empty;
                worksheet.Cell(row, 6).Value = GetTripTypeText(booking.TripType);
                worksheet.Cell(row, 7).Value = GetVehicleTypeText(booking.VehicleTypeRequested);
                worksheet.Cell(row, 8).Value = GetServiceTypeText(booking.IsPersonal, booking.IsExternalRental);
                worksheet.Cell(row, 9).Value = GetBookingStatusText(booking.Status);
                worksheet.Cell(row, 10).Value = booking.PickupLocation;
                worksheet.Cell(row, 11).Value = booking.DestinationLocation;
                worksheet.Cell(row, 12).Value = booking.Purpose ?? string.Empty;
                worksheet.Cell(row, 13).Value = booking.PassengerCount;
                worksheet.Cell(row, 14).Value = booking.JobNo ?? string.Empty;
                worksheet.Cell(row, 15).Value = booking.CostCenter ?? string.Empty;
                worksheet.Cell(row, 16).Value = booking.SONo ?? string.Empty;
                worksheet.Cell(row, 17).Value = booking.AssignedVehicle?.PlateNo ?? booking.ExternalRental?.RentalPlateNo ?? string.Empty;
                worksheet.Cell(row, 18).Value = booking.AssignedDriver?.User?.UsernameTH ?? booking.AssignedDriver?.User?.UsernameEN ?? booking.ExternalRental?.RentalDriverName ?? string.Empty;
                worksheet.Cell(row, 19).Value = booking.ExternalRental?.VendorName ?? string.Empty;
                worksheet.Cell(row, 20).Value = booking.ExternalRental?.QuotedPrice;
                worksheet.Cell(row, 21).Value = booking.CreatedAtUtc.ToLocalTime();
            }

            worksheet.Column(2).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            worksheet.Column(3).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            worksheet.Column(21).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            worksheet.Column(20).Style.NumberFormat.Format = "#,##0.00";
            worksheet.SheetView.FreezeRows(1);
            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"booking-report-{BuildDateRangeFileSuffix(startDate, endDate)}.xlsx";
            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // GET: /Admin/Booking/{id}
        /*[HttpGet]
        public async Task<IActionResult> Booking(long id)
        {
            ViewData["ActiveNav"] = "AdminWorklist";

            var booking = await _db.Bookings
                .Include(b => b.Requester)
                .Include(b => b.Attachments)
                .Include(b => b.Approvals)
                .Include(b => b.ExternalRental)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking == null) return NotFound();

            return View(booking);
        }*/

        // POST: /Admin/SendVendorQuote/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendVendorQuote(long id, VendorQuoteModel model)
        {
            var booking = await _db.Bookings
                .Include(b => b.ExternalRental)
                .Include(b => b.Requester)
                .SingleOrDefaultAsync(b => b.BookingId == id);
            if (booking == null) return NotFound();

            if (model == null)
            {
                TempData["Error"] = "Invalid vendor data.";
                return RedirectToAction(nameof(Detail), new { id });
            }

            // Server-side validation (never rely on client-side only): vendor name is required and
            // the quoted price must be a number greater than 0. Reject without saving otherwise.
            var vendorName = model.VendorName?.Trim();
            if (string.IsNullOrWhiteSpace(vendorName))
            {
                TempData["Error"] = "กรุณาเลือกผู้ให้บริการ";
                return RedirectToAction(nameof(Detail), new { id });
            }
            if (model.QuotedPrice is null)
            {
                TempData["Error"] = "กรุณากรอกราคา";
                return RedirectToAction(nameof(Detail), new { id });
            }
            if (model.QuotedPrice <= 0)
            {
                TempData["Error"] = "ราคาต้องมากกว่า 0";
                return RedirectToAction(nameof(Detail), new { id });
            }

            model.VendorName = vendorName;
            var now = DateTime.UtcNow;

            booking.ExternalRental = new ExternalRental
            {
                BookingId = booking.BookingId,
                VendorName = model.VendorName,
                QuotedPrice = model.QuotedPrice,
                QuoteSentAtUtc = now,
                UserDecision = ExternalUserDecision.Pending
            };
            _db.ExternalRentals.Add(booking.ExternalRental);

            booking.IsExternalRental = true;
            booking.Status = BookingStatus.WaitingUserVendorAccept;
            booking.UpdatedAtUtc = now;

            await _db.SaveChangesAsync();

            try
            {
                if (!string.IsNullOrWhiteSpace(booking.Requester?.Email))
                {
                    await _emailNotifications.NotifyActionRequiredAsync(
                            booking,
                            new[] { booking.Requester.Email },
                            "กรุณายอมรับหรือปฏิเสธผู้ให้บริการ",
                            relativeUrl: $"/Booking/Detail/{booking.BookingId}");

                    TempData["Success"] = "บันทึกราคารถภายนอกเรียบร้อย";
                }
                else
                {
                    TempData["Error"] = "Failed to send email. Requester email not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email for booking VS-{BookingId}", booking.BookingId);
                TempData["Error"] = "Failed to send email. See logs.";
            }

            return RedirectToAction(nameof(Detail), new { id });
        }

        // GET helper: redirect GET requests for SendVendorQuote to booking page
        [HttpGet("/Admin/SendVendorQuote/{id:long}")]
        public IActionResult SendVendorQuoteGet(long id)
        {
            return RedirectToAction(nameof(Detail), new { id });
        }

        // POST: /Admin/ConfirmVendor/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmVendor(long id, VendorConfirmModel model)
        {
            var booking = await _db.Bookings.Include(b => b.ExternalRental).SingleOrDefaultAsync(b => b.BookingId == id);
            if (booking == null) return NotFound();

            // Server-side validation (never rely on client-side only): plate, driver name and phone
            // are all required. Trim before checking. Reject without creating/updating otherwise.
            if (model == null)
            {
                TempData["Error"] = "ข้อมูลไม่ครบถ้วน";
                return RedirectToAction(nameof(Detail), new { id });
            }

            var plateNo = model.RentalPlateNo?.Trim();
            var driverName = model.RentalDriverName?.Trim();
            var driverPhone = model.RentalDriverPhone?.Trim();

            if (string.IsNullOrWhiteSpace(plateNo))
            {
                TempData["Error"] = "กรุณากรอกทะเบียนรถ";
                return RedirectToAction(nameof(Detail), new { id });
            }
            if (string.IsNullOrWhiteSpace(driverName))
            {
                TempData["Error"] = "กรุณากรอกชื่อผู้ขับ";
                return RedirectToAction(nameof(Detail), new { id });
            }
            if (string.IsNullOrWhiteSpace(driverPhone))
            {
                TempData["Error"] = "กรุณากรอกเบอร์โทรติดต่อ";
                return RedirectToAction(nameof(Detail), new { id });
            }

            if (booking.ExternalRental == null)
            {
                booking.ExternalRental = new ExternalRental { BookingId = booking.BookingId };
                _db.ExternalRentals.Add(booking.ExternalRental);
            }

            booking.ExternalRental.RentalPlateNo = plateNo;
            booking.ExternalRental.RentalDriverName = driverName;
            booking.ExternalRental.RentalDriverPhone = driverPhone;
            booking.ExternalRental.AdminClosedAtUtc = null; // reopen

            booking.IsExternalRental = true;
            booking.UpdatedAtUtc = DateTime.UtcNow;
            booking.Status = BookingStatus.AdminActionRequired;

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Detail), new { id });
        }

        // POST: /Admin/ForceComplete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceComplete(long id)
        {
            var booking = await _db.Bookings
                .Include(b => b.ExternalRental)
                .Include(b => b.Requester)
                .SingleOrDefaultAsync(b => b.BookingId == id);
            if (booking == null) return NotFound();

            booking.Status = BookingStatus.Completed;
            booking.UpdatedAtUtc = DateTime.UtcNow;

            if (booking.ExternalRental != null)
            {
                booking.ExternalRental.AdminClosedAtUtc = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(booking.Requester?.Email))
            {
                try
                {
                    await _emailNotifications.NotifyStatusChangedAsync(
                        booking,
                        Array.Empty<string>(),
                        booking.Requester.Email,
                        statusChangedAtUtc: booking.UpdatedAtUtc,
                        relativeUrl: $"/Booking/Detail/{booking.BookingId}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send force complete email for booking {BookingId}", booking.BookingId);
                }
            }

            return RedirectToAction(nameof(Detail), new { id });
        }

        // ===== Master Data =====

        // GET: /Admin/Vehicles
        [HttpGet]
        public async Task<IActionResult> Vehicles()
        {
            ViewData["ActiveNav"] = "AdminVehicles";
            var vehicles = await _db.Vehicles
                .AsNoTracking()
                .Include(v => v.Driver)
                    .ThenInclude(d => d.User)
                .OrderBy(v => v.PlateNo)
                .ToListAsync();
            return View(vehicles);
        }

        // GET: /Admin/vehicles/create
        [HttpGet("/Admin/vehicles/create")]
        public IActionResult VehicleCreate()
        {
            ViewData["ActiveNav"] = "AdminVehicles";
            return View("VehicleForm", new VehicleVm { IsActive = true });
        }

        // POST: /Admin/vehicles/create
        [HttpPost("/Admin/vehicles/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VehicleCreate([FromForm] VehicleVm vm)
        {
            ViewData["ActiveNav"] = "AdminVehicles";
            if (!ModelState.IsValid) return View("VehicleForm", vm);

            // unique plate
            var exists = await _db.Vehicles.AnyAsync(v => v.PlateNo == vm.PlateNo);
            if (exists)
            {
                ModelState.AddModelError(nameof(vm.PlateNo), "Plate number already exists.");
                return View("VehicleForm", vm);
            }

            var vehicle = new Vehicle
            {
                PlateNo = vm.PlateNo!.Trim(),
                VehicleType = vm.VehicleType,
                Status = vm.Status,
                IsActive = vm.IsActive,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.Vehicles.Add(vehicle);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Vehicle created.";
            return RedirectToAction(nameof(Vehicles));
        }

        // GET: /Admin/vehicles/edit/{id}
        [HttpGet("/Admin/vehicles/edit/{id:int}")]
        public async Task<IActionResult> VehicleEdit(int id)
        {
            ViewData["ActiveNav"] = "AdminVehicles";
            var v = await _db.Vehicles.AsNoTracking().SingleOrDefaultAsync(x => x.VehicleId == id);
            if (v == null) return NotFound();

            var vm = new VehicleVm
            {
                VehicleId = v.VehicleId,
                PlateNo = v.PlateNo,
                VehicleType = v.VehicleType,
                Status = v.Status,
                IsActive = v.IsActive
            };

            return View("VehicleForm", vm);
        }

        // POST: /Admin/vehicles/edit/{id}
        [HttpPost("/Admin/vehicles/edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VehicleEdit(int id, [FromForm] VehicleVm vm)
        {
            ViewData["ActiveNav"] = "AdminVehicles";
            if (!ModelState.IsValid) return View("VehicleForm", vm);

            var v = await _db.Vehicles.SingleOrDefaultAsync(x => x.VehicleId == id);
            if (v == null) return NotFound();

            if (!string.Equals(v.PlateNo, vm.PlateNo, StringComparison.OrdinalIgnoreCase))
            {
                var exists = await _db.Vehicles.AnyAsync(x => x.PlateNo == vm.PlateNo && x.VehicleId != id);
                if (exists)
                {
                    ModelState.AddModelError(nameof(vm.PlateNo), "Plate number already exists.");
                    return View("VehicleForm", vm);
                }
            }

            v.PlateNo = vm.PlateNo!.Trim();
            v.VehicleType = vm.VehicleType;
            v.Status = vm.Status;
            v.IsActive = vm.IsActive;
            v.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Vehicle updated.";
            return RedirectToAction(nameof(Vehicles));
        }

        // POST: /Admin/vehicles/delete/{id}
        [HttpPost("/Admin/vehicles/delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VehicleDelete(int id)
        {
            var v = await _db.Vehicles.SingleOrDefaultAsync(x => x.VehicleId == id);
            if (v == null) return NotFound();

            // soft-delete
            v.IsActive = false;
            v.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            TempData["Success"] = "Vehicle deactivated.";
            return RedirectToAction(nameof(Vehicles));
        }

        // GET: /Admin/Drivers
        [HttpGet]
        public async Task<IActionResult> Drivers()
        {
            ViewData["ActiveNav"] = "AdminDrivers";
            var drivers = await _db.Drivers
                .AsNoTracking()
                .Include(d => d.User)
                .Include(d => d.Vehicle)
                .OrderBy(d => d.DriverId)
                .ToListAsync();

            return View(drivers);
        }

        // GET: /Admin/Drivers/Detail/{id}
        [HttpGet("/Admin/Drivers/Detail/{id:int}")]
        public async Task<IActionResult> DriverDetail(
            int id,
            string? search,
            BookingStatus? status,
            TripType? tripType,
            string? serviceType,
            DateTime? startDate,
            DateTime? endDate,
            int page = 1,
            int pageSize = 20)
        {
            ViewData["ActiveNav"] = "AdminDrivers";

            var driver = await _db.Drivers
                .AsNoTracking()
                .Include(d => d.User)
                .Include(d => d.Vehicle)
                .SingleOrDefaultAsync(d => d.DriverId == id);

            if (driver is null) return NotFound();

            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

            var trimmedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var trimmedServiceType = string.IsNullOrWhiteSpace(serviceType) ? null : serviceType.Trim().ToLowerInvariant();

            var vm = new DriverDetailVm
            {
                DriverId = driver.DriverId,
                DriverName = driver.User.UsernameTH ?? driver.User.UsernameEN ?? driver.User.UserCode,
                DriverNameEn = driver.User.UsernameEN,
                UserCode = driver.User.UserCode,
                PhoneNo = driver.Phone,
                IsActive = driver.IsActive,
                CanDriveOutOfProvince = driver.CanDriveOutOfProvince,
                VehicleType = driver.Vehicle != null ? GetVehicleTypeText(driver.Vehicle.VehicleType) : null,
                PlateNo = driver.Vehicle?.PlateNo,
                Search = trimmedSearch,
                Status = status,
                TripType = tripType,
                ServiceType = trimmedServiceType,
                StartDate = startDate,
                EndDate = endDate,
                PageSize = pageSize
            };

            await PopulateDriverRatingSummaryAsync(vm, id);

            if (startDate.HasValue && endDate.HasValue && endDate.Value.Date < startDate.Value.Date)
            {
                TempData["Error"] = "วันที่สิ้นสุดต้องมากกว่าหรือเท่ากับวันที่เริ่มต้น";
                vm.Page = 1;
                vm.TotalCount = 0;
                vm.TotalPages = 0;
                return View(vm);
            }

            // Query pipeline (server-side, in order): scope to this driver -> apply search ->
            // apply other filters -> count for paging -> order -> skip/take -> project.
            var query = BuildDriverBookingsQuery(id, trimmedSearch, status, tripType, trimmedServiceType, startDate, endDate);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages > 0 && page > totalPages) page = totalPages;

            var rawRows = await query
                .OrderByDescending(b => b.StartAtUtc)
                .ThenByDescending(b => b.BookingId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new
                {
                    b.BookingId,
                    b.JobNo,
                    b.TripType,
                    b.Status,
                    b.IsPersonal,
                    b.IsExternalRental,
                    b.PickupLocation,
                    b.DestinationLocation,
                    b.StartAtUtc,
                    b.EndAtUtc,
                    RequesterName = b.Requester.UsernameTH,
                    InternalPlateNo = b.AssignedVehicle != null ? b.AssignedVehicle.PlateNo : null,
                    RentalPlateNo = b.ExternalRental != null ? b.ExternalRental.RentalPlateNo : null,
                    RatingScore = b.Rating != null
                        ? (double?)((b.Rating.Score1 + b.Rating.Score2 + b.Rating.Score3 + b.Rating.Score4 + b.Rating.Score5) / 5.0)
                        : null
                })
                .ToListAsync();

            // In-memory mapping stage: formatting/truncation is plain C#, not translatable to SQL,
            // so it happens here — once, after materialization — rather than in the view.
            vm.Bookings = rawRows.Select(b => new DriverBookingRowVm
            {
                BookingId = b.BookingId,
                JobNo = b.JobNo,
                RequesterName = b.RequesterName,
                TripType = b.TripType,
                ServiceTypeText = GetServiceTypeText(b.IsPersonal, b.IsExternalRental),
                PlateNo = b.IsExternalRental ? b.RentalPlateNo : b.InternalPlateNo,
                PickupDisplay = Truncate(b.PickupLocation, 50),
                DestinationDisplay = Truncate(b.DestinationLocation, 50),
                StartDisplay = b.StartAtUtc.ToLocalTime().ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture),
                EndDisplay = b.EndAtUtc.ToLocalTime().ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture),
                Status = b.Status,
                RatingScore = b.RatingScore
            }).ToList();

            vm.Page = page;
            vm.TotalCount = totalCount;
            vm.TotalPages = totalPages;

            return View(vm);
        }

        // Shared server-side filter builder for the driver-scoped booking list on Driver Detail.
        // Query order: scope to driver -> search -> all other filters. A numeric/"VS-123" search
        // takes an exact-match fast path on the indexed primary key instead of being OR'd together
        // with the text Contains() clauses (which would force a full scan across many columns).
        private IQueryable<Booking> BuildDriverBookingsQuery(
            int driverId,
            string? search,
            BookingStatus? status,
            TripType? tripType,
            string? serviceType,
            DateTime? startDate,
            DateTime? endDate)
        {
            var query = _db.Bookings
                .AsNoTracking()
                .Where(b => b.AssignedDriverId == driverId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();
                var idKeyword = keyword.StartsWith("VS-", StringComparison.OrdinalIgnoreCase)
                    ? keyword["VS-".Length..]
                    : keyword;

                if (long.TryParse(idKeyword, out var bookingId))
                {
                    query = query.Where(b => b.BookingId == bookingId);
                }
                else
                {
                    query = query.Where(b =>
                        (b.JobNo != null && b.JobNo.Contains(keyword)) ||
                        (b.Purpose != null && b.Purpose.Contains(keyword)) ||
                        b.PickupLocation.Contains(keyword) ||
                        b.DestinationLocation.Contains(keyword) ||
                        (b.Requester.UsernameTH != null && b.Requester.UsernameTH.Contains(keyword)) ||
                        (b.Requester.UsernameEN != null && b.Requester.UsernameEN.Contains(keyword)) ||
                        b.Requester.UserCode.Contains(keyword) ||
                        (b.AssignedVehicle != null && b.AssignedVehicle.PlateNo.Contains(keyword)) ||
                        (b.ExternalRental != null && b.ExternalRental.RentalPlateNo != null && b.ExternalRental.RentalPlateNo.Contains(keyword)));
                }
            }

            if (status.HasValue)
            {
                query = query.Where(b => b.Status == status.Value);
            }

            if (tripType.HasValue)
            {
                query = query.Where(b => b.TripType == tripType.Value);
            }

            if (serviceType == "personal")
            {
                query = query.Where(b => b.IsPersonal);
            }
            else if (serviceType == "external")
            {
                query = query.Where(b => !b.IsPersonal && b.IsExternalRental);
            }
            else if (serviceType == "company")
            {
                query = query.Where(b => !b.IsPersonal && !b.IsExternalRental);
            }

            var (startUtc, endExclusiveUtc) = BuildUtcDateRange(startDate, endDate);
            if (startUtc.HasValue)
            {
                query = query.Where(b => b.StartAtUtc >= startUtc.Value);
            }

            if (endExclusiveUtc.HasValue)
            {
                query = query.Where(b => b.StartAtUtc < endExclusiveUtc.Value);
            }

            return query;
        }

        // Lifetime rating summary for the driver (independent of the booking list's search/filter/
        // pagination) — average/highest/lowest score plus the 5 most recent rating comments.
        private async Task PopulateDriverRatingSummaryAsync(DriverDetailVm vm, int driverId)
        {
            var ratingScoresQuery = _db.DriverRatings
                .AsNoTracking()
                .Where(r => r.DriverId == driverId)
                .Select(r => (double)(r.Score1 + r.Score2 + r.Score3 + r.Score4 + r.Score5) / 5.0);

            vm.RatingCount = await ratingScoresQuery.CountAsync();

            if (vm.RatingCount > 0)
            {
                vm.AverageRating = await ratingScoresQuery.AverageAsync();
                vm.HighestRating = await ratingScoresQuery.MaxAsync();
                vm.LowestRating = await ratingScoresQuery.MinAsync();
            }

            vm.CompletedJobCount = await _db.Bookings.AsNoTracking().CountAsync(b =>
                b.AssignedDriverId == driverId &&
                (b.Status == BookingStatus.Completed || b.Status == BookingStatus.Rated));

            var rawRatings = await _db.DriverRatings
                .AsNoTracking()
                .Where(r => r.DriverId == driverId)
                .OrderByDescending(r => r.CreatedAtUtc)
                .Take(5)
                .Select(r => new
                {
                    r.BookingId,
                    r.Booking.JobNo,
                    EvaluatorName = r.Booking.Requester.UsernameTH,
                    Score = (r.Score1 + r.Score2 + r.Score3 + r.Score4 + r.Score5) / 5.0,
                    r.Comment,
                    r.CreatedAtUtc
                })
                .ToListAsync();

            vm.RecentRatings = rawRatings.Select(r => new DriverRatingRowVm
            {
                BookingId = r.BookingId,
                JobNo = r.JobNo,
                EvaluatorName = r.EvaluatorName,
                Score = r.Score,
                Comment = r.Comment,
                RatedAtDisplay = r.CreatedAtUtc.ToLocalTime().ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture)
            }).ToList();
        }

        // Read-only ViewModel for /Admin/Drivers/Detail/{id} — never send the Driver/Booking/
        // DriverRating entity graph directly into the view.
        public sealed class DriverDetailVm
        {
            public int DriverId { get; set; }

            public string DriverName { get; set; } = string.Empty;
            public string? DriverNameEn { get; set; }
            public string? UserCode { get; set; }
            public string? PhoneNo { get; set; }
            public bool IsActive { get; set; }
            public bool CanDriveOutOfProvince { get; set; }

            public string? VehicleType { get; set; }
            public string? PlateNo { get; set; }

            public double? AverageRating { get; set; }
            public int RatingCount { get; set; }
            public double? HighestRating { get; set; }
            public double? LowestRating { get; set; }
            public int CompletedJobCount { get; set; }

            public List<DriverBookingRowVm> Bookings { get; set; } = new();
            public List<DriverRatingRowVm> RecentRatings { get; set; } = new();

            public int Page { get; set; }
            public int PageSize { get; set; }
            public int TotalCount { get; set; }
            public int TotalPages { get; set; }

            public string? Search { get; set; }
            public BookingStatus? Status { get; set; }
            public TripType? TripType { get; set; }
            public string? ServiceType { get; set; }
            public DateTime? StartDate { get; set; }
            public DateTime? EndDate { get; set; }
        }

        public sealed class DriverBookingRowVm
        {
            public long BookingId { get; set; }
            public string? JobNo { get; set; }
            public string? RequesterName { get; set; }
            public TripType TripType { get; set; }
            public string ServiceTypeText { get; set; } = "";
            public string? PlateNo { get; set; }
            public string PickupDisplay { get; set; } = "";
            public string DestinationDisplay { get; set; } = "";
            public string StartDisplay { get; set; } = "";
            public string EndDisplay { get; set; } = "";
            public BookingStatus Status { get; set; }
            public double? RatingScore { get; set; }
        }

        public sealed class DriverRatingRowVm
        {
            public long BookingId { get; set; }
            public string? JobNo { get; set; }
            public string? EvaluatorName { get; set; }
            public double Score { get; set; }
            public string? Comment { get; set; }
            public string RatedAtDisplay { get; set; } = "";
        }

        // GET: /Admin/drivers/edit/{id}
        [HttpGet("/Admin/drivers/edit/{id:int}")]
        public async Task<IActionResult> EditDriver(int id)
        {
            ViewData["ActiveNav"] = "AdminDrivers";
            var d = await _db.Drivers.Include(x => x.User).SingleOrDefaultAsync(x => x.DriverId == id);
            if (d == null) return NotFound();

            ViewBag.DriverId = d.DriverId; // provide id for form action in view

            ViewBag.Vehicles = await _db.Vehicles.AsNoTracking()
                .Where(v => v.IsActive &&
                            (!_db.Drivers.Any(dr => dr.VehicleId == v.VehicleId && dr.IsActive) ||
                             v.VehicleId == d.VehicleId))
                .OrderBy(v => v.PlateNo)
                .Select(v => new SelectListItem { Value = v.VehicleId.ToString(), Text = v.PlateNo + " • " + v.VehicleType.ToString() })
                .ToListAsync();

            var vm = new CreateDriverVm
            {
                DriverCode = d.User.UserCode,
                DriverNameTH = d.User.UsernameTH,
                DriverNameEN = d.User.UsernameEN,
                Phone = d.Phone,
                CanDriveOutOfProvince = d.CanDriveOutOfProvince,
                VehicleId = d.VehicleId,
                IsActive = d.IsActive
            };

            return View("EditDriver", vm);
        }

        // POST: /Admin/drivers/update/{id}
        [HttpPost("/Admin/drivers/update/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDriver(int id, [FromForm] CreateDriverVm vm)
        {
            ViewData["ActiveNav"] = "AdminDrivers";
            if (!ModelState.IsValid)
            {
                ViewBag.Vehicles = await _db.Vehicles.AsNoTracking()
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.PlateNo)
                    .Select(v => new SelectListItem { Value = v.VehicleId.ToString(), Text = v.PlateNo + " • " + v.VehicleType.ToString() })
                    .ToListAsync();
                return View("EditDriver", vm);
            }

            var d = await _db.Drivers.Include(x => x.User).SingleOrDefaultAsync(x => x.DriverId == id);
            if (d == null) return NotFound();

            // update user
            d.User.UsernameTH = vm.DriverNameTH ?? d.User.UsernameTH;
            d.User.UsernameEN = vm.DriverNameEN ?? d.User.UsernameEN;
            d.User.UpdatedAtUtc = DateTime.UtcNow;

            // update driver row
            d.VehicleId = vm.VehicleId;
            d.IsActive = vm.IsActive;
            d.Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone.Trim();
            d.CanDriveOutOfProvince = vm.CanDriveOutOfProvince;
            d.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Driver updated.";
            return RedirectToAction(nameof(Drivers));
        }

        // POST: /Admin/drivers/delete/{id}
        [HttpPost("/Admin/drivers/delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDriver(int id)
        {
            var d = await _db.Drivers.Include(x => x.User).SingleOrDefaultAsync(x => x.DriverId == id);
            if (d == null) return NotFound();

            d.IsActive = false;
            d.UpdatedAtUtc = DateTime.UtcNow;
            d.User.IsActive = false;
            d.User.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Driver deactivated.";
            return RedirectToAction(nameof(Drivers));
        }

        // GET: /Admin/Users
        [HttpGet]
        public async Task<IActionResult> Users(string? q, bool roleUser = false, bool roleAdmin = false, bool roleDriver = false, bool roleApprover = false, int page = 1, int pageSize = 20)
        {
            ViewData["ActiveNav"] = "AdminUsers";

            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 20 : pageSize;

            var query = _db.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(u =>
                    u.UserCode.Contains(term) ||
                    (u.UsernameTH != null && u.UsernameTH.Contains(term)) ||
                    (u.UsernameEN != null && u.UsernameEN.Contains(term)) ||
                    (u.FunctionAbbr != null && u.FunctionAbbr.Contains(term)) ||
                    (u.DeptAbbr != null && u.DeptAbbr.Contains(term)) ||
                    (u.DivAbbr != null && u.DivAbbr.Contains(term)));
            }

            var selectedRoleMask = 0;
            if (roleUser) selectedRoleMask |= ROLE_USER;
            if (roleAdmin) selectedRoleMask |= ROLE_ADMIN;
            if (roleDriver) selectedRoleMask |= ROLE_DRIVER;
            if (roleApprover) selectedRoleMask |= ROLE_APPROVER;

            if (selectedRoleMask != 0)
            {
                query = query.Where(u => (u.RoleFlags & selectedRoleMask) != 0);
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages > 0 && page > totalPages) page = totalPages;

            var users = await query
                .OrderBy(u => u.UserCode)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new UsersListVm
            {
                Users = users,
                Query = q,
                RoleUser = roleUser,
                RoleAdmin = roleAdmin,
                RoleDriver = roleDriver,
                RoleApprover = roleApprover,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };

            return View(vm);
        }

        public class UsersListVm
        {
            public IList<User> Users { get; set; } = new List<User>();
            public string? Query { get; set; }
            public bool RoleUser { get; set; }
            public bool RoleAdmin { get; set; }
            public bool RoleDriver { get; set; }
            public bool RoleApprover { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
            public int TotalCount { get; set; }
            public int TotalPages { get; set; }
        }

        // GET: /Admin/users/edit/{id}
        [HttpGet("/Admin/users/edit/{id:int}")]
        public async Task<IActionResult> EditUserRole(int id)
        {
            ViewData["ActiveNav"] = "AdminUsers";

            var user = await _db.Users
                .AsNoTracking()
                .Include(u => u.Credential)
                .SingleOrDefaultAsync(u => u.UserId == id);
            if (user == null) return NotFound();

            var vm = new EditUserRoleVm
            {
                UserId = user.UserId,
                UserCode = user.UserCode,
                UsernameTH = user.UsernameTH,
                UsernameEN = user.UsernameEN,
                RoleUser = (user.RoleFlags & ROLE_USER) != 0,
                RoleAdmin = (user.RoleFlags & ROLE_ADMIN) != 0,
                RoleApprover = (user.RoleFlags & ROLE_APPROVER) != 0,
                IsDriver = (user.RoleFlags & ROLE_DRIVER) != 0,
                HasCredential = user.Credential != null
            };

            return View("EditUserRole", vm);
        }

        // POST: /Admin/users/edit/{id}
        [HttpPost("/Admin/users/edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUserRole(int id, [FromForm] EditUserRoleVm vm)
        {
            ViewData["ActiveNav"] = "AdminUsers";

            var user = await _db.Users
                .Include(u => u.Credential)
                .SingleOrDefaultAsync(u => u.UserId == id);
            if (user == null) return NotFound();

            var isDriver = (user.RoleFlags & ROLE_DRIVER) != 0;
            if (isDriver)
            {
                TempData["Error"] = "Driver role cannot be edited.";
                return RedirectToAction(nameof(EditUserRole), new { id });
            }

            var roleFlags = 0;
            if (vm.RoleUser) roleFlags |= ROLE_USER;
            if (vm.RoleAdmin) roleFlags |= ROLE_ADMIN;
            if (vm.RoleApprover) roleFlags |= ROLE_APPROVER;

            if (roleFlags == 0)
            {
                ModelState.AddModelError(string.Empty, "At least one role is required.");
            }

            if (!ModelState.IsValid)
            {
                vm.UserId = user.UserId;
                vm.UserCode = user.UserCode;
                vm.UsernameTH = user.UsernameTH;
                vm.UsernameEN = user.UsernameEN;
                vm.IsDriver = isDriver;
                vm.HasCredential = user.Credential != null;
                return View("EditUserRole", vm);
            }

            user.RoleFlags = roleFlags;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            TempData["Success"] = "User role updated.";
            return RedirectToAction(nameof(Users));
        }

        public class EditUserRoleVm
        {
            public int UserId { get; set; }
            public string UserCode { get; set; } = string.Empty;
            public string? UsernameTH { get; set; }
            public string? UsernameEN { get; set; }
            public bool RoleUser { get; set; }
            public bool RoleAdmin { get; set; }
            public bool RoleApprover { get; set; }
            public bool IsDriver { get; set; }
            public bool HasCredential { get; set; }
        }

        // GET: /Admin/users/create
        [HttpGet("/Admin/users/create")]
        public IActionResult CreateUser()
        {
            ViewData["ActiveNav"] = "AdminUsers";
            return View(new CreateUserVm
            {
                RoleUser = true,
                IsActive = true
            });
        }

        // POST: /Admin/users/create
        [HttpPost("/Admin/users/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser([FromForm] CreateUserVm vm)
        {
            ViewData["ActiveNav"] = "AdminUsers";

            var userCode = (vm.UserCode ?? string.Empty).Trim();
            var usernameTh = (vm.UsernameTH ?? string.Empty).Trim();
            var usernameEn = string.IsNullOrWhiteSpace(vm.UsernameEN) ? null : vm.UsernameEN.Trim();
            var email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim();

            if (string.IsNullOrWhiteSpace(userCode))
            {
                ModelState.AddModelError(nameof(vm.UserCode), "User code is required.");
            }

            if (string.IsNullOrWhiteSpace(usernameTh))
            {
                ModelState.AddModelError(nameof(vm.UsernameTH), "Username TH is required.");
            }

            var roleFlags = 0;
            if (vm.RoleUser) roleFlags |= ROLE_USER;
            if (vm.RoleAdmin) roleFlags |= ROLE_ADMIN;
            if (vm.RoleApprover) roleFlags |= ROLE_APPROVER;

            if (roleFlags == 0)
            {
                ModelState.AddModelError(string.Empty, "At least one role is required.");
            }

            if (!string.IsNullOrWhiteSpace(userCode) && await _db.Users.AnyAsync(u => u.UserCode == userCode))
            {
                ModelState.AddModelError(nameof(vm.UserCode), "This user code already exists.");
            }

            if (!string.IsNullOrWhiteSpace(email) && await _db.Users.AnyAsync(u => u.Email == email))
            {
                ModelState.AddModelError(nameof(vm.Email), "This email already exists.");
            }

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var now = DateTime.UtcNow;
            var user = new User
            {
                UserCode = userCode,
                UsernameTH = usernameTh,
                UsernameEN = usernameEn,
                Email = email,
                FunctionAbbr = string.IsNullOrWhiteSpace(vm.FunctionAbbr) ? null : vm.FunctionAbbr.Trim(),
                DeptAbbr = string.IsNullOrWhiteSpace(vm.DeptAbbr) ? null : vm.DeptAbbr.Trim(),
                DivAbbr = string.IsNullOrWhiteSpace(vm.DivAbbr) ? null : vm.DivAbbr.Trim(),
                PositionTH = string.IsNullOrWhiteSpace(vm.PositionTH) ? null : vm.PositionTH.Trim(),
                PositionEN = string.IsNullOrWhiteSpace(vm.PositionEN) ? null : vm.PositionEN.Trim(),
                RoleFlags = roleFlags,
                IsActive = vm.IsActive,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"User created: {userCode}";
            return RedirectToAction(nameof(Users));
        }

        // POST: /Admin/users/reset-password/{id}
        [HttpPost("/Admin/users/reset-password/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetUserPassword(int id)
        {
            var user = await _db.Users
                .Include(u => u.Credential)
                .SingleOrDefaultAsync(u => u.UserId == id);

            if (user?.Credential == null) return NotFound();

            var now = DateTime.UtcNow;
            var hashed = _hasher.Hash("Welcome@123");

            var credential = user.Credential;
            credential.PasswordHash = hashed.Hash;
            credential.PasswordSalt = hashed.Salt;
            credential.PasswordAlgo = hashed.Algo;
            credential.Iterations = hashed.Iterations;
            credential.PasswordChangedAtUtc = null;
            credential.FailedCount = 0;
            credential.LastFailedAtUtc = null;
            credential.IsLocked = false;
            credential.UpdatedAtUtc = now;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Password reset to Welcome@123.";
            return RedirectToAction(nameof(EditUserRole), new { id });
        }

        // POST: /Admin/UpdateUsers
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUsers()
        {
            ViewData["ActiveNav"] = "AdminUsers";
            try
            {
                using var client = new HttpClient();
                var endpoint = new Uri("https://serv03.gcmeapps.com/edm/api/GetEmployeeData/1");
                var response = await client.GetAsync(endpoint);

                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = "API call failed.";
                    return RedirectToAction(nameof(Users));
                }

                var jsonString = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var apiUsers = JsonSerializer.Deserialize<List<UsersApi>>(jsonString, options) ?? new List<UsersApi>();

                bool IsApproverPosition(string? positionEn)
                {
                    var pos = (positionEn ?? string.Empty).Trim();
                    if (pos.StartsWith("Acting ", StringComparison.OrdinalIgnoreCase))
                    {
                        pos = pos.Substring("Acting ".Length).Trim();
                    }

                    return pos.Equals("Section Manager", StringComparison.OrdinalIgnoreCase)
                        || pos.Equals("Division Manager", StringComparison.OrdinalIgnoreCase)
                        || pos.Equals("Vice President", StringComparison.OrdinalIgnoreCase)
                        || pos.Equals("Deputy Managing Director", StringComparison.OrdinalIgnoreCase)
                        || pos.Equals("Managing Director", StringComparison.OrdinalIgnoreCase);
                }

                var now = DateTime.UtcNow;
                var apiUserCodes = new HashSet<string>(
                    apiUsers.Select(u => (u.EmployeeID ?? string.Empty).Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x)),
                    StringComparer.OrdinalIgnoreCase);

                var dbUsers = await _db.Users.ToListAsync();
                var dbUsersByCode = dbUsers.ToDictionary(u => u.UserCode, StringComparer.OrdinalIgnoreCase);
                var lineManagerUpdates = new List<(User User, string? ManagerCode)>();

                foreach (var item in apiUsers)
                {
                    var userCode = (item.EmployeeID ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(userCode))
                    {
                        continue;
                    }

                    var nameTh = string.Join(" ", new[] { item.PreName, item.FirstName, item.LastName }
                        .Where(x => !string.IsNullOrWhiteSpace(x)));
                    if (string.IsNullOrWhiteSpace(nameTh))
                    {
                        nameTh = userCode;
                    }

                    var nameEn = string.IsNullOrWhiteSpace(item.NameEng) ? null : item.NameEng.Trim();
                    var shouldBeApprover = IsApproverPosition(item.PositionText100EN);

                    if (dbUsersByCode.TryGetValue(userCode, out var user))
                    {
                        user.UsernameTH = nameTh;
                        user.UsernameEN = nameEn;
                        user.FunctionTH = item.FunctionTH;
                        user.FunctionEN = item.FunctionEN;
                        user.FunctionAbbr = item.FunctionAbbrEN;
                        user.DeptTH = item.DeptTH;
                        user.DeptEN = item.DeptEN;
                        user.DeptAbbr = item.DeptAbbrEN;
                        user.DivTH = item.DivTH;
                        user.DivEN = item.DivEN;
                        user.DivAbbr = item.DivAbbrEN;
                        user.Email = item.Email;
                        user.PositionTH = item.PositionText100TH;
                        user.PositionEN = item.PositionText100EN;
                        if (shouldBeApprover && (user.RoleFlags & ROLE_APPROVER) == 0)
                        {
                            user.RoleFlags |= ROLE_APPROVER;
                        }
                        user.IsActive = true;
                        user.UpdatedAtUtc = now;
                    }
                    else
                    {
                        user = new User
                        {
                            UserCode = userCode,
                            UsernameTH = nameTh,
                            UsernameEN = nameEn,
                            FunctionTH = item.FunctionTH,
                            FunctionEN = item.FunctionEN,
                            FunctionAbbr = item.FunctionAbbrEN,
                            DeptTH = item.DeptTH,
                            DeptEN = item.DeptEN,
                            DeptAbbr = item.DeptAbbrEN,
                            DivTH = item.DivTH,
                            DivEN = item.DivEN,
                            DivAbbr = item.DivAbbrEN,
                            Email = item.Email,
                            PositionTH = item.PositionText100TH,
                            PositionEN = item.PositionText100EN,
                            RoleFlags = ROLE_USER | (shouldBeApprover ? ROLE_APPROVER : 0),
                            IsActive = true,
                            CreatedAtUtc = now,
                            UpdatedAtUtc = now
                        };

                        _db.Users.Add(user);
                        dbUsers.Add(user);
                        dbUsersByCode[userCode] = user;
                    }

                    lineManagerUpdates.Add((user, item.LineManagerID?.Trim()));
                }

                var protectedRoles = ROLE_DRIVER;
                foreach (var user in dbUsers)
                {
                    if ((user.RoleFlags & protectedRoles) != 0)
                    {
                        continue;
                    }

                    var shouldBeActive = apiUserCodes.Contains(user.UserCode);
                    if (user.IsActive != shouldBeActive)
                    {
                        user.IsActive = shouldBeActive;
                        user.UpdatedAtUtc = now;
                    }
                }

                await _db.SaveChangesAsync();

                foreach (var (user, managerCode) in lineManagerUpdates)
                {
                    int? managerId = null;
                    if (!string.IsNullOrWhiteSpace(managerCode) && dbUsersByCode.TryGetValue(managerCode, out var manager))
                    {
                        managerId = manager.UserId;
                    }

                    if (user.LineManagerId != managerId)
                    {
                        user.LineManagerId = managerId;
                        user.UpdatedAtUtc = now;
                    }
                }

                await _db.SaveChangesAsync();

                TempData["Success"] = $"Users updated ({apiUserCodes.Count} active).";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update users from API.");
                TempData["Error"] = "Failed to update users.";
            }

            return RedirectToAction(nameof(Users));
        }

        // ===== Approvers =====
        // GET: /Admin/approvers/create
        [HttpGet("/Admin/approvers/create")]
        public async Task<IActionResult> CreateApprover()
        {
            ViewData["ActiveNav"] = "AdminUsers";
            await Task.CompletedTask;
            return View(new CreateApproverVm { IsActive = true });
        }

        // POST: /Admin/approvers/create
        [HttpPost("/Admin/approvers/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateApprover([FromForm] CreateApproverVm vm)
        {
            ViewData["ActiveNav"] = "AdminUsers";

            if (!ModelState.IsValid)
                return View(vm);

            var userCode = (vm.ApproverCode ?? "").Trim(); // เช่น APRV001
            if (string.IsNullOrWhiteSpace(userCode))
            {
                ModelState.AddModelError(nameof(vm.ApproverCode), "Approver code is required.");
                return View(vm);
            }

            // validate not exists
            var existsUser = await _db.Users.AnyAsync(u => u.UserCode == userCode);
            if (existsUser)
            {
                ModelState.AddModelError(nameof(vm.ApproverCode), "This ApproverCode already exists.");
                return View(vm);
            }

            // create user (approver)
            var user = new User
            {
                UserCode = userCode,
                UsernameTH = vm.ApproverNameTH?.Trim() ?? userCode,
                UsernameEN = vm.ApproverNameEN?.Trim(),
                Email = null,
                RoleFlags = ROLE_APPROVER,
                IsActive = vm.IsActive,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync(); // get UserId

            // credential
            var initialPassword = string.IsNullOrWhiteSpace(vm.InitialPassword) ? "Welcome@123" : vm.InitialPassword;
            var hashed = _hasher.Hash(initialPassword);

            var cred = new UserCredential
            {
                UserId = user.UserId,
                LoginUsername = userCode,
                PasswordHash = hashed.Hash,
                PasswordSalt = hashed.Salt,
                PasswordAlgo = hashed.Algo,
                Iterations = hashed.Iterations,
                IsLocked = false,
                FailedCount = 0,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                PasswordChangedAtUtc = null
            };

            _db.UserCredentials.Add(cred);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Approver created: {userCode} (initial password: {initialPassword})";
            return RedirectToAction(nameof(CreateApprover));
        }

        // POST: /Admin/UpdateRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserRole(int userId, int roleFlags)
        {
            var u = await _db.Users.SingleOrDefaultAsync(x => x.UserId == userId);
            if (u == null) return NotFound();
            u.RoleFlags = roleFlags;
            u.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Users));
        }


        [HttpGet("/Admin/drivers/create")]
        public async Task<IActionResult> CreateDriver()
        {
            ViewData["ActiveNav"] = "AdminDrivers";

            ViewBag.Vehicles = await _db.Vehicles.AsNoTracking()
                .Where(v => v.IsActive && !_db.Drivers.Any(d => d.VehicleId == v.VehicleId && d.IsActive))
                .OrderBy(v => v.PlateNo)
                .Select(v => new SelectListItem
                {
                    Value = v.VehicleId.ToString(),
                    Text = v.PlateNo + " • " + v.VehicleType.ToString()
                }).ToListAsync();
            return View(new CreateDriverVm { IsActive = true });
        }

        
        // POST: /admin/drivers/create
        [HttpPost("/Admin/drivers/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDriver([FromForm] CreateDriverVm vm)
        {
            ViewData["ActiveNav"] = "AdminDrivers";

            if (!ModelState.IsValid)
            {
                ViewBag.Vehicles = await _db.Vehicles.AsNoTracking()
                    .Where(v => v.IsActive && !_db.Drivers.Any(d => d.VehicleId == v.VehicleId && d.IsActive))
                    .OrderBy(v => v.PlateNo)
                    .Select(v => new SelectListItem
                    {
                        Value = v.VehicleId.ToString(),
                        Text = v.PlateNo + " • " + v.VehicleType.ToString()
                    }).ToListAsync();
                return View(vm);
            }

            var userCode = (vm.DriverCode ?? "").Trim(); // เช่น DRV001
            if (string.IsNullOrWhiteSpace(userCode))
            {
                ModelState.AddModelError(nameof(vm.DriverCode), "Driver code is required.");
                ViewBag.Vehicles = await _db.Vehicles.AsNoTracking()
                    .Where(v => v.IsActive && !_db.Drivers.Any(d => d.VehicleId == v.VehicleId && d.IsActive))
                    .OrderBy(v => v.PlateNo)
                    .Select(v => new SelectListItem
                    {
                        Value = v.VehicleId.ToString(),
                        Text = v.PlateNo + " • " + v.VehicleType.ToString()
                    }).ToListAsync();
                return View(vm);
            }

            // 1) validate not exists
            var existsUser = await _db.Users.AnyAsync(u => u.UserCode == userCode);
            if (existsUser)
            {
                ModelState.AddModelError(nameof(vm.DriverCode), "This DriverCode already exists.");
                ViewBag.Vehicles = await _db.Vehicles.AsNoTracking()
                    .Where(v => v.IsActive && !_db.Drivers.Any(d => d.VehicleId == v.VehicleId && d.IsActive))
                    .OrderBy(v => v.PlateNo)
                    .Select(v => new SelectListItem
                    {
                        Value = v.VehicleId.ToString(),
                        Text = v.PlateNo + " • " + v.VehicleType.ToString()
                    }).ToListAsync();
                return View(vm);
            }

            // 2) validate vehicle exists
            var vehicle = await _db.Vehicles.SingleOrDefaultAsync(v => v.VehicleId == vm.VehicleId && v.IsActive);
            if (vehicle is null)
            {
                ModelState.AddModelError(nameof(vm.VehicleId), "Vehicle not found or inactive.");
                ViewBag.Vehicles = await _db.Vehicles.AsNoTracking()
                    .Where(v => v.IsActive && !_db.Drivers.Any(d => d.VehicleId == v.VehicleId && d.IsActive))
                    .OrderBy(v => v.PlateNo)
                    .Select(v => new SelectListItem
                    {
                        Value = v.VehicleId.ToString(),
                        Text = v.PlateNo + " • " + v.VehicleType.ToString()
                    }).ToListAsync();
                return View(vm);
            }

            var hasActiveDriver = await _db.Drivers.AnyAsync(d => d.VehicleId == vm.VehicleId && d.IsActive);
            if (hasActiveDriver)
            {
                ModelState.AddModelError(nameof(vm.VehicleId), "Vehicle already has an active driver.");
                ViewBag.Vehicles = await _db.Vehicles.AsNoTracking()
                    .Where(v => v.IsActive && !_db.Drivers.Any(d => d.VehicleId == v.VehicleId && d.IsActive))
                    .OrderBy(v => v.PlateNo)
                    .Select(v => new SelectListItem
                    {
                        Value = v.VehicleId.ToString(),
                        Text = v.PlateNo + " • " + v.VehicleType.ToString()
                    }).ToListAsync();
                return View(vm);
            }

            // 3) create user (driver user)
            var user = new User
            {
                UserCode = userCode,
                UsernameTH = vm.DriverNameTH?.Trim() ?? userCode,
                UsernameEN = vm.DriverNameEN?.Trim(),
                Email = null,
                RoleFlags = ROLE_DRIVER,  // ✅ driver only
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync(); // get UserId

            // 4) create credential (loginUsername = driverCode)
            var initialPassword = string.IsNullOrWhiteSpace(vm.InitialPassword)
                ? "Welcome@123"
                : vm.InitialPassword;

            var hashed = _hasher.Hash(initialPassword);

            var cred = new UserCredential
            {
                UserId = user.UserId,
                LoginUsername = userCode,             // ✅ driver login ด้วย DRV001
                PasswordHash = hashed.Hash,
                PasswordSalt = hashed.Salt,
                PasswordAlgo = hashed.Algo,
                Iterations = hashed.Iterations,
                IsLocked = false,
                FailedCount = 0,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                PasswordChangedAtUtc = null           // ✅ ยังไม่เคยเปลี่ยน => บังคับเปลี่ยนตอน login
            };
            _db.UserCredentials.Add(cred);

            // 5) create driver row
            var driver = new Driver
            {
                UserId = user.UserId,
                VehicleId = vm.VehicleId,
                Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone.Trim(),
                CanDriveOutOfProvince = vm.CanDriveOutOfProvince,
                IsActive = true,
                LastAssignedAtUtc = null,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _db.Drivers.Add(driver);

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Driver created: {userCode} (initial password: {initialPassword})";
            return RedirectToAction(nameof(Drivers));
        }

        // POST: /Admin/drivers/reset-password/{id}
        [HttpPost("/Admin/drivers/reset-password/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetDriverPassword(int id)
        {
            var driver = await _db.Drivers
                .Include(d => d.User)
                .ThenInclude(u => u.Credential)
                .SingleOrDefaultAsync(d => d.DriverId == id);

            if (driver?.User == null) return NotFound();

            var now = DateTime.UtcNow;
            var hashed = _hasher.Hash("Welcome@123");

            var credential = driver.User.Credential;
            if (credential == null)
            {
                credential = new UserCredential
                {
                    UserId = driver.User.UserId,
                    LoginUsername = driver.User.UserCode,
                    CreatedAtUtc = now
                };
                _db.UserCredentials.Add(credential);
            }

            credential.PasswordHash = hashed.Hash;
            credential.PasswordSalt = hashed.Salt;
            credential.PasswordAlgo = hashed.Algo;
            credential.Iterations = hashed.Iterations;
            credential.PasswordChangedAtUtc = null;
            credential.FailedCount = 0;
            credential.LastFailedAtUtc = null;
            credential.IsLocked = false;
            credential.UpdatedAtUtc = now;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Password reset to Welcome@123.";
            return RedirectToAction(nameof(EditDriver), new { id });
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
                .Include(b => b.CompletionPhotos)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking is null) return NotFound();

            // mark layout active nav for Detail -> MyTask
            ViewData["ActiveNav"] = "AllTasks";

            // Load available driver/vehicle pairs for reassignment card
            var terminal = BookingStatusHelper.TerminalStatuses;
            bool showReassign = !booking.IsExternalRental
                                && !booking.IsPersonal
                                && !terminal.Contains(booking.Status)
                                && booking.VehicleTypeRequested != VehicleType.Electric;

            if (showReassign)
            {
                var startUtc     = booking.StartAtUtc;
                var endUtc       = booking.EndAtUtc;
                var reqType      = booking.VehicleTypeRequested;
                bool needOutProv = booking.TripType == TripType.OutProvince;

                var candidateData = await (
                    from d in _db.Drivers.AsNoTracking()
                    join v in _db.Vehicles.AsNoTracking() on d.VehicleId equals v.VehicleId
                    join u in _db.Users.AsNoTracking()    on d.UserId  equals u.UserId
                    where d.IsActive
                          && (!needOutProv || d.CanDriveOutOfProvince)
                          && v.IsActive
                          && v.VehicleType == reqType
                    select new
                    {
                        d.DriverId,
                        d.VehicleId,
                        DriverName = u.UsernameTH ?? u.UsernameEN,
                        v.PlateNo,
                        v.VehicleType
                    }
                ).ToListAsync();

                if (candidateData.Count > 0)
                {
                    var driverIds  = candidateData.Select(c => c.DriverId).ToList();
                    var vehicleIds = candidateData.Select(c => c.VehicleId).ToList();

                    var busyAssignments = await _db.Bookings.AsNoTracking()
                        .Where(b =>
                            b.BookingId != booking.BookingId &&
                            !terminal.Contains(b.Status) &&
                            b.StartAtUtc < endUtc &&
                            startUtc < b.EndAtUtc &&
                            ((b.AssignedVehicleId != null && vehicleIds.Contains(b.AssignedVehicleId.Value)) ||
                             (b.AssignedDriverId  != null && driverIds.Contains(b.AssignedDriverId.Value))))
                        .Select(b => new { b.AssignedVehicleId, b.AssignedDriverId })
                        .ToListAsync();

                    var busyVehicleSet = new HashSet<int>(busyAssignments
                        .Where(x => x.AssignedVehicleId.HasValue)
                        .Select(x => x.AssignedVehicleId!.Value));
                    var busyDriverSet = new HashSet<int>(busyAssignments
                        .Where(x => x.AssignedDriverId.HasValue)
                        .Select(x => x.AssignedDriverId!.Value));

                    ViewBag.AvailableDrivers = candidateData
                        .Where(c => !busyVehicleSet.Contains(c.VehicleId) && !busyDriverSet.Contains(c.DriverId))
                        .Select(c =>
                        {
                            var vt = c.VehicleType switch
                            {
                                VehicleType.Pickup => "รถกระบะ",
                                VehicleType.Van    => "รถตู้",
                                VehicleType.Sedan  => "รถเก๋ง",
                                _                  => c.VehicleType.ToString()
                            };
                            return new SelectListItem(
                                $"{c.DriverName ?? $"#{c.DriverId}"} — {c.PlateNo} ({vt})",
                                c.DriverId.ToString(),
                                c.DriverId == booking.AssignedDriverId);
                        })
                        .ToList();
                }
                else
                {
                    ViewBag.AvailableDrivers = new List<SelectListItem>();
                }
            }

            ViewBag.ShowReassign = showReassign;

            return View(booking);
        }

        // POST: /Admin/ReassignDriver/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReassignDriver(long id, int driverId)
        {
            var booking = await _db.Bookings.SingleOrDefaultAsync(b => b.BookingId == id);
            if (booking is null) return NotFound();

            var terminal = BookingStatusHelper.TerminalStatuses;
            if (booking.IsExternalRental || booking.IsPersonal
                || terminal.Contains(booking.Status)
                || booking.VehicleTypeRequested == VehicleType.Electric)
            {
                TempData["Error"] = "ไม่สามารถเปลี่ยน พขร./รถ ได้ในสถานะนี้";
                return RedirectToAction(nameof(Detail), new { id });
            }

            var driver = await _db.Drivers
                .Include(d => d.Vehicle)
                .FirstOrDefaultAsync(d => d.DriverId == driverId && d.IsActive);

            if (driver is null || driver.Vehicle is null || !driver.Vehicle.IsActive)
            {
                TempData["Error"] = "ข้อมูล พขร./รถ ไม่ถูกต้อง";
                return RedirectToAction(nameof(Detail), new { id });
            }

            if (driver.Vehicle.VehicleType != booking.VehicleTypeRequested)
            {
                TempData["Error"] = "ข้อมูล พขร./รถ ไม่ถูกต้อง";
                return RedirectToAction(nameof(Detail), new { id });
            }

            // Server-side availability re-check (same pattern as TryAssignFirstAvailableCompanyVehicleAsync)
            var startUtc = booking.StartAtUtc;
            var endUtc   = booking.EndAtUtc;
            bool isBusy  = await _db.Bookings.AsNoTracking().AnyAsync(b =>
                b.BookingId != booking.BookingId &&
                !terminal.Contains(b.Status) &&
                b.StartAtUtc < endUtc &&
                startUtc < b.EndAtUtc &&
                ((b.AssignedVehicleId != null && b.AssignedVehicleId == driver.VehicleId) ||
                 (b.AssignedDriverId  != null && b.AssignedDriverId  == driver.DriverId)));

            if (isBusy)
            {
                TempData["Error"] = "พขร./รถ ที่เลือกไม่ว่างในช่วงวันและเวลาที่จอง กรุณาเลือกใหม่";
                return RedirectToAction(nameof(Detail), new { id });
            }

            int? oldDriverId = booking.AssignedDriverId;
            bool driverChanged = oldDriverId != driver.DriverId;

            booking.AssignedDriverId  = driver.DriverId;
            booking.AssignedVehicleId = driver.VehicleId;
            booking.UpdatedAtUtc      = DateTime.UtcNow;

            if (driverChanged)
            {
                booking.Status = BookingStatus.WaitingDriverAccept;
            }

            await _db.SaveChangesAsync();

            if (driverChanged)
            {
                try { await _driverBookingNotifications.NotifyAdminReassignedToNewDriverAsync(booking.BookingId, oldDriverId, driver.DriverId); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to notify new driver for booking {BookingId}", booking.BookingId); }

                if (oldDriverId.HasValue)
                {
                    try { await _driverBookingNotifications.NotifyAdminReassignedAwayFromOldDriverAsync(booking.BookingId, oldDriverId.Value, driver.DriverId); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Failed to notify old driver for booking {BookingId}", booking.BookingId); }
                }
            }

            TempData["Success"] = "เปลี่ยน พขร./รถ เรียบร้อยแล้ว";
            return RedirectToAction(nameof(Detail), new { id });
        }

        // GET: /Booking/Queue
        [HttpGet]
        public async Task<IActionResult> Queue(DateTime? startDate)
        {
            ViewData["ActiveNav"] = "Queue";

            var baseDate = startDate ?? DateTime.Today;
            var monthStart = new DateTime(baseDate.Year, baseDate.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var vehicles = await _db.Vehicles
                .AsNoTracking()
                .Where(v => v.IsActive)
                .OrderBy(v => v.VehicleType)
                .ThenBy(v => v.PlateNo)
                .ToListAsync();

            var vehicleIds = vehicles.Select(v => v.VehicleId).ToList();
            var terminal = BookingStatusHelper.TerminalStatuses;

            var bookings = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.Requester)
                .Where(b => b.AssignedVehicleId != null
                            && vehicleIds.Contains(b.AssignedVehicleId.Value)
                            && !terminal.Contains(b.Status)
                            && b.StartAtUtc < monthEnd.ToUniversalTime()
                            && b.EndAtUtc > monthStart.ToUniversalTime())
                .OrderBy(b => b.StartAtUtc)
                .ToListAsync();

            var externalBookings = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.Requester)
                .Include(b => b.ExternalRental)
                .Where(b => (b.IsExternalRental || b.ExternalRental != null)
                            && !terminal.Contains(b.Status)
                            && b.StartAtUtc < monthEnd.ToUniversalTime()
                            && b.EndAtUtc > monthStart.ToUniversalTime())
                .OrderBy(b => b.StartAtUtc)
                .ToListAsync();

            string BuildExternalLabel(Booking booking)
            {
                var plate = booking.ExternalRental?.RentalPlateNo;
                if (!string.IsNullOrWhiteSpace(plate))
                {
                    return $"รถภายนอก - {plate.Trim()}";
                }

                var vendor = booking.ExternalRental?.VendorName;
                if (!string.IsNullOrWhiteSpace(vendor))
                {
                    return $"รถภายนอก - {vendor.Trim()}";
                }

                return "รถภายนอก";
            }

            var schedules = vehicles.Select(v => new VehicleScheduleVm
            {
                Vehicle = v,
                Slots = bookings
                    .Where(b => b.AssignedVehicleId == v.VehicleId)
                    .Select(b => new BookingSlotVm
                    {
                        BookingId = b.BookingId,
                        StartLocal = b.StartAtUtc.ToLocalTime(),
                        EndLocal = b.EndAtUtc.ToLocalTime(),
                        RequesterName = b.Requester?.UsernameTH ?? b.Requester?.UsernameEN ?? b.RequesterUserId.ToString(),
                        IsExternalRental = false
                    })
                    .ToList()
            }).ToList();

            var externalSchedules = externalBookings
                .GroupBy(b => b.VehicleTypeRequested)
                .Select(group => new VehicleScheduleVm
                {
                    Vehicle = new Vehicle
                    {
                        VehicleId = -1000 - (int)group.Key,
                        PlateNo = "รถภายนอก",
                        VehicleType = group.Key,
                        Status = VehicleStatus.Available,
                        IsActive = true,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    },
                    Slots = group.Select(b => new BookingSlotVm
                    {
                        BookingId = b.BookingId,
                        StartLocal = b.StartAtUtc.ToLocalTime(),
                        EndLocal = b.EndAtUtc.ToLocalTime(),
                        RequesterName = b.Requester?.UsernameTH ?? b.Requester?.UsernameEN ?? b.RequesterUserId.ToString(),
                        DisplayLabel = BuildExternalLabel(b),
                        IsExternalRental = true
                    }).ToList()
                })
                .ToList();

            schedules.AddRange(externalSchedules);

            var vm = new QueueVm
            {
                VehicleType = VehicleType.Sedan,
                StartDate = monthStart,
                Days = DateTime.DaysInMonth(monthStart.Year, monthStart.Month),
                Vehicles = schedules
            };

            return View(vm);
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
                //.AsNoTracking()
                .Include(b => b.Requester)
                .Where(b =>
                    b.RequesterUserId == userId ||
                    b.AssignedDriverId == userId ||
                    b.Approvals.Any(a => a.ApproverUserId == userId))
                .OrderByDescending(b => b.StartAtUtc)
                .ToListAsync();

            return View(bookings);
        }


        public class CreateDriverVm
        {
            [Required]
            public string? DriverCode { get; set; } // DRV001

            public string? DriverNameTH { get; set; }
            public string? DriverNameEN { get; set; }

            public string? Phone { get; set; }

            public bool CanDriveOutOfProvince { get; set; }

            [Required]
            public int VehicleId { get; set; }

            public bool IsActive { get; set; } = true;

            public string? InitialPassword { get; set; } // default Welcome@123
        }

        // CreateApprover VM
        public class CreateApproverVm
        {
            [Required]
            public string? ApproverCode { get; set; } // e.g. APRV001

            public string? ApproverNameTH { get; set; }
            public string? ApproverNameEN { get; set; }

            public bool IsActive { get; set; } = true;

            public string? InitialPassword { get; set; }
        }

        public class CreateUserVm
        {
            [Required]
            public string? UserCode { get; set; }

            [Required]
            public string? UsernameTH { get; set; }

            public string? UsernameEN { get; set; }
            public string? Email { get; set; }
            public string? FunctionAbbr { get; set; }
            public string? DeptAbbr { get; set; }
            public string? DivAbbr { get; set; }
            public string? PositionTH { get; set; }
            public string? PositionEN { get; set; }
            public bool RoleUser { get; set; } = true;
            public bool RoleAdmin { get; set; }
            public bool RoleApprover { get; set; }
            public bool IsActive { get; set; } = true;
        }

        // Vehicle VM
        public class VehicleVm
        {
            public int VehicleId { get; set; }

            [Required]
            public string? PlateNo { get; set; }

            public VehicleType VehicleType { get; set; } = VehicleType.Sedan;

            public VehicleStatus Status { get; set; } = VehicleStatus.Available;

            public bool IsActive { get; set; } = true;
        }

        // ===== Models =====
        public class VendorQuoteModel
        {
            [Required]
            public string? VendorName { get; set; }

            [Required]
            [Range(0.01, double.MaxValue)]
            public decimal? QuotedPrice { get; set; }

            public string? Note { get; set; }
        }

        public class VendorConfirmModel
        {
            [Required]
            public string? RentalPlateNo { get; set; }

            [Required]
            public string? RentalDriverName { get; set; }

            [Required]
            public string? RentalDriverPhone { get; set; }
        }

        public class UsersApi
        {
            public string? EmployeeID { get; set; }
            public string? PreName { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? NameEng { get; set; }
            public string? FunctionTH { get; set; }
            public string? FunctionEN { get; set; }
            public string? FunctionAbbrEN { get; set; }
            public string? DeptTH { get; set; }
            public string? DeptEN { get; set; }
            public string? DeptAbbrEN { get; set; }
            public string? DivTH { get; set; }
            public string? DivEN { get; set; }
            public string? DivAbbrEN { get; set; }
            public string? Email { get; set; }
            public string? PositionText100TH { get; set; }
            public string? PositionText100EN { get; set; }
            public string? LineManagerID { get; set; }
        }

        // POST: /Admin/ApprovePersonal/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePersonal(long id)
        {
            var booking = await _db.Bookings
                .Include(b => b.Requester)
                .SingleOrDefaultAsync(b => b.BookingId == id);

            if (booking == null) return NotFound();

            if (!booking.IsPersonal || booking.Status != BookingStatus.WaitingAdminPersonal)
            {
                TempData["Error"] = "Booking is not waiting for personal vehicle approval.";
                return RedirectToAction(nameof(Detail), new { id });
            }

            booking.Status = BookingStatus.WaitingApproval;
            booking.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            var adminEmails = await _db.Users
                .AsNoTracking()
                .Where(u => (u.RoleFlags & ROLE_ADMIN) != 0 && u.Email != null)
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

                if (!string.IsNullOrWhiteSpace(booking.Requester?.Email))
                {
                    await _emailNotifications.NotifyStatusChangedAsync(
                        booking,
                        Array.Empty<string>(),
                        booking.Requester.Email,
                        statusChangedAtUtc: booking.UpdatedAtUtc,
                        relativeUrl: $"/Booking/Detail/{booking.BookingId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send personal booking status email for booking {BookingId}", booking.BookingId);
            }

            await CreateApprovalsFromLineManagerChainAsync(booking.BookingId, booking.RequesterUserId);

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
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send approval notification for booking {BookingId}", booking.BookingId);
            }

            TempData["Success"] = "Personal vehicle approved and sent for approvals.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        // POST: /Admin/RejectPersonal/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPersonal(long id)
        {
            var booking = await _db.Bookings
                .Include(b => b.Requester)
                .SingleOrDefaultAsync(b => b.BookingId == id);

            if (booking == null) return NotFound();

            if (!booking.IsPersonal || booking.Status != BookingStatus.WaitingAdminPersonal)
            {
                TempData["Error"] = "Booking is not waiting for personal vehicle approval.";
                return RedirectToAction(nameof(Detail), new { id });
            }

            booking.Status = BookingStatus.Rejected;
            booking.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(booking.Requester?.Email))
            {
                try
                {
                    await _emailNotifications.NotifyStatusChangedAsync(
                        booking,
                        Array.Empty<string>(),
                        booking.Requester.Email,
                        statusChangedAtUtc: booking.UpdatedAtUtc,
                        relativeUrl: $"/Booking/Detail/{booking.BookingId}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send personal booking reject email for booking {BookingId}", booking.BookingId);
                }
            }

            TempData["Success"] = "Personal vehicle request rejected.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        // POST: /Admin/RejectBooking/{id}
        // Admin override: reject a booking in any non-terminal step (including steps where the
        // admin is not the current actor). Reject in the admin's own step is handled elsewhere.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectBooking(long id)
        {
            var booking = await _db.Bookings
                .Include(b => b.Requester)
                .SingleOrDefaultAsync(b => b.BookingId == id);

            if (booking == null) return NotFound();

            if (BookingStatusHelper.TerminalStatuses.Contains(booking.Status))
            {
                TempData["Error"] = "ใบงานนี้อยู่ในสถานะสิ้นสุดแล้ว ไม่สามารถปฏิเสธได้";
                return RedirectToAction(nameof(Detail), new { id });
            }

            var assignedDriverId = booking.AssignedDriverId;

            booking.Status = BookingStatus.Rejected;
            booking.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(booking.Requester?.Email))
            {
                try
                {
                    await _emailNotifications.NotifyStatusChangedAsync(
                        booking,
                        Array.Empty<string>(),
                        booking.Requester.Email,
                        statusChangedAtUtc: booking.UpdatedAtUtc,
                        relativeUrl: $"/Booking/Detail/{booking.BookingId}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send reject email for booking {BookingId}", booking.BookingId);
                }
            }

            if (assignedDriverId.HasValue)
            {
                try
                {
                    await _driverBookingNotifications.NotifyBookingCancelledAsync(booking.BookingId, assignedDriverId.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to notify driver about rejected booking {BookingId}", booking.BookingId);
                }
            }

            TempData["Success"] = "ปฏิเสธใบงานเรียบร้อยแล้ว";
            return RedirectToAction(nameof(Detail), new { id });
        }

        private async Task CreateApprovalsFromLineManagerChainAsync(long bookingId, int requesterUserId)
        {
            var now = DateTime.UtcNow;

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

            // approver chain created -> booking now waits for approval decision
            bookingForApproval.Status = BookingStatus.WaitingApproval;
            bookingForApproval.UpdatedAtUtc = now;

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

        private static (DateTime? StartUtc, DateTime? EndExclusiveUtc) BuildUtcDateRange(DateTime? startDate, DateTime? endDate)
        {
            DateTime? startUtc = null;
            DateTime? endExclusiveUtc = null;

            if (startDate.HasValue)
            {
                startUtc = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Local).ToUniversalTime();
            }

            if (endDate.HasValue)
            {
                endExclusiveUtc = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
            }

            return (startUtc, endExclusiveUtc);
        }

        private async Task PopulateBookingFilterOptionsAsync()
        {
            var users = _db.Users.AsNoTracking().AsQueryable();

            ViewBag.FunctionOptions = await users
                .Where(u => u.FunctionAbbr != null && u.FunctionAbbr != "")
                .Select(u => u.FunctionAbbr!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            ViewBag.DeptOptions = await users
                .Where(u => u.DeptAbbr != null && u.DeptAbbr != "")
                .Select(u => u.DeptAbbr!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            ViewBag.DivOptions = await users
                .Where(u => u.DivAbbr != null && u.DivAbbr != "")
                .Select(u => u.DivAbbr!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
        }

        // Shared server-side filter builder for /Admin/AllBookings and /Admin/ExportBookingsExcel.
        // Query order: base table -> search -> all other filters. Callers apply OrderBy/Skip/Take
        // (and any additional Include for their own projection) on the returned IQueryable.
        private IQueryable<Booking> BuildBookingsFilterQuery(
            string? search,
            BookingStatus? status,
            TripType? tripType,
            string? serviceType,
            string? functionAbbr,
            string? deptAbbr,
            string? divAbbr,
            DateTime? startDate,
            DateTime? endDate)
        {
            var query = _db.Bookings
                .AsNoTracking()
                .Include(b => b.Requester)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();
                var idKeyword = keyword.StartsWith("VS-", StringComparison.OrdinalIgnoreCase)
                    ? keyword["VS-".Length..]
                    : keyword;
                var hasNumericId = long.TryParse(idKeyword, out var idValue);

                query = query.Where(b =>
                    (hasNumericId && b.BookingId == idValue) ||
                    (b.JobNo != null && b.JobNo.Contains(keyword)) ||
                    (b.Purpose != null && b.Purpose.Contains(keyword)) ||
                    b.PickupLocation.Contains(keyword) ||
                    b.DestinationLocation.Contains(keyword) ||
                    (b.Requester.UsernameTH != null && b.Requester.UsernameTH.Contains(keyword)) ||
                    (b.Requester.UsernameEN != null && b.Requester.UsernameEN.Contains(keyword)) ||
                    b.Requester.UserCode.Contains(keyword) ||
                    (b.AssignedVehicle != null && b.AssignedVehicle.PlateNo.Contains(keyword)) ||
                    (b.AssignedDriver != null && b.AssignedDriver.User.UsernameTH != null && b.AssignedDriver.User.UsernameTH.Contains(keyword)) ||
                    (b.AssignedDriver != null && b.AssignedDriver.User.UsernameEN != null && b.AssignedDriver.User.UsernameEN.Contains(keyword)) ||
                    (b.ExternalRental != null && b.ExternalRental.RentalPlateNo != null && b.ExternalRental.RentalPlateNo.Contains(keyword)) ||
                    (b.ExternalRental != null && b.ExternalRental.VendorName != null && b.ExternalRental.VendorName.Contains(keyword)));
            }

            if (status.HasValue)
            {
                query = query.Where(b => b.Status == status.Value);
            }

            if (tripType.HasValue)
            {
                query = query.Where(b => b.TripType == tripType.Value);
            }

            // serviceType mirrors the same "company / external / personal" grouping the page used
            // to compute client-side from IsPersonal/IsExternalRental.
            if (serviceType == "personal")
            {
                query = query.Where(b => b.IsPersonal);
            }
            else if (serviceType == "external")
            {
                query = query.Where(b => !b.IsPersonal && b.IsExternalRental);
            }
            else if (serviceType == "company")
            {
                query = query.Where(b => !b.IsPersonal && !b.IsExternalRental);
            }

            if (!string.IsNullOrWhiteSpace(functionAbbr))
            {
                var term = functionAbbr.Trim();
                query = query.Where(b => b.Requester.FunctionAbbr == term);
            }

            if (!string.IsNullOrWhiteSpace(deptAbbr))
            {
                var term = deptAbbr.Trim();
                query = query.Where(b => b.Requester.DeptAbbr == term);
            }

            if (!string.IsNullOrWhiteSpace(divAbbr))
            {
                var term = divAbbr.Trim();
                query = query.Where(b => b.Requester.DivAbbr == term);
            }

            var (startUtc, endExclusiveUtc) = BuildUtcDateRange(startDate, endDate);
            if (startUtc.HasValue)
            {
                query = query.Where(b => b.StartAtUtc >= startUtc.Value);
            }

            if (endExclusiveUtc.HasValue)
            {
                query = query.Where(b => b.StartAtUtc < endExclusiveUtc.Value);
            }

            return query;
        }

        // Aggregates status counts for the filtered result set (not just the current page) via a
        // single GroupBy query, so the stat tiles reflect the same filter as the grid/pagination.
        private static async Task<(int All, int Pending, int Completed, int Bad)> GetBookingStatusStatsAsync(IQueryable<Booking> filteredQuery)
        {
            var completedStatuses = new[] { BookingStatus.Completed, BookingStatus.Rated };
            var badStatuses = new[] { BookingStatus.Rejected, BookingStatus.Cancelled };

            var statusCounts = await filteredQuery
                .GroupBy(b => b.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var all = statusCounts.Sum(x => x.Count);
            var completed = statusCounts.Where(x => completedStatuses.Contains(x.Status)).Sum(x => x.Count);
            var bad = statusCounts.Where(x => badStatuses.Contains(x.Status)).Sum(x => x.Count);
            var pending = all - completed - bad;

            return (all, pending, completed, bad);
        }

        private static string BuildDateRangeFileSuffix(DateTime? startDate, DateTime? endDate)
        {
            if (startDate.HasValue && endDate.HasValue)
            {
                return $"{startDate.Value:yyyyMMdd}-{endDate.Value:yyyyMMdd}";
            }

            if (startDate.HasValue)
            {
                return $"from-{startDate.Value:yyyyMMdd}";
            }

            if (endDate.HasValue)
            {
                return $"until-{endDate.Value:yyyyMMdd}";
            }

            return DateTime.Today.ToString("yyyyMMdd");
        }

        private static string GetTripTypeText(TripType tripType) => tripType switch
        {
            TripType.InProvince => "ในจังหวัด",
            TripType.OutProvince => "นอกจังหวัด",
            _ => tripType.ToString()
        };

        private static string GetVehicleTypeText(VehicleType vehicleType) => vehicleType switch
        {
            VehicleType.Pickup => "รถกระบะ",
            VehicleType.Van => "รถตู้",
            VehicleType.Sedan => "รถเก๋ง",
            VehicleType.Electric => "รถไฟฟ้า",
            _ => vehicleType.ToString()
        };

        private static string GetServiceTypeText(bool isPersonal, bool isExternalRental)
        {
            if (isPersonal)
            {
                return "รถส่วนตัว";
            }

            if (isExternalRental)
            {
                return "รถภายนอกบริษัท";
            }

            return "รถในบริษัท";
        }

        // Truncates display text to maxLength characters, appending "..." when cut. Never renders
        // "null"/empty text — falls back to "-" so table layout doesn't collapse.
        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            value = value.Trim();

            return value.Length <= maxLength
                ? value
                : value[..maxLength] + "...";
        }

        private static string GetBookingStatusText(BookingStatus status) => status switch
        {
            BookingStatus.Draft => "ร่าง",
            BookingStatus.Submitted => "ส่งคำขอแล้ว",
            BookingStatus.WaitingApproval => "รออนุมัติ",
            BookingStatus.WaitingDriverAccept => "รอพนักงานขับรถตอบรับ",
            BookingStatus.DriverAccepted => "พนักงานขับรถตอบรับ",
            BookingStatus.ApprovedSelfDrive => "อนุมัติแล้ว",
            BookingStatus.WaitingAdminVendorQuotation => "รอเสนอราคาผู้ให้บริการ",
            BookingStatus.WaitingUserVendorAccept => "รอผู้ขอใช้ยอมรับผู้ให้บริการ",
            BookingStatus.WaitingAdminVendorConfirm => "รอยืนยันผู้ให้บริการ",
            BookingStatus.VendorRejectedByUser => "ผู้ขอใช้ปฏิเสธผู้ให้บริการ",
            BookingStatus.WaitingAdminPersonal => "รอผู้ดูแลอนุมัติรถส่วนตัว",
            BookingStatus.Completed => "เสร็จสิ้น",
            BookingStatus.Rated => "ให้คะแนนแล้ว",
            BookingStatus.Rejected => "ถูกปฏิเสธ",
            BookingStatus.Cancelled => "ยกเลิก",
            BookingStatus.AdminActionRequired => "รอผู้ดูแลดำเนินการ",
            _ => status.ToString()
        };
    }
}
