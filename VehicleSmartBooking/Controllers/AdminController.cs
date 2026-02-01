using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Services;

namespace VehicleSmartBooking.Controllers
{

    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly VehicleBookingDbContext _db;
        private readonly ILogger<AdminController> _logger;
        private readonly IPasswordHasher _hasher;

        private const int ROLE_DRIVER = 4;
        private const int ROLE_ADMIN = 2;

        public AdminController(VehicleBookingDbContext db, ILogger<AdminController> logger, IPasswordHasher hasher)
        {
            _db = db;
            _logger = logger;
            _hasher = hasher;
        }

        // ===== Operations =====

        // GET: /Admin/Worklist
        [HttpGet]
        public async Task<IActionResult> Worklist()
        {
            ViewData["ActiveNav"] = "AdminWorklist";

            // TODO:
            // - bookings with status WaitingAdminVendorQuotation / WaitingAdminVendorConfirm / AdminActionRequired
            await Task.CompletedTask;
            return View();
        }

        // GET: /Admin/Booking/{id}
        [HttpGet]
        public async Task<IActionResult> Booking(long id)
        {
            ViewData["ActiveNav"] = "AdminWorklist";

            // TODO: load booking + external rental + approvals
            await Task.CompletedTask;
            return View();
        }

        // POST: /Admin/SendVendorQuote/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendVendorQuote(long id, VendorQuoteModel model)
        {
            // TODO:
            // - upsert ExternalRentals row
            // - set Booking.IsExternalRental = true
            // - set status -> WaitingUserVendorAccept
            await Task.CompletedTask;
            return RedirectToAction(nameof(Booking), new { id });
        }

        // POST: /Admin/ConfirmVendor/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmVendor(long id, VendorConfirmModel model)
        {
            // TODO:
            // - fill ExternalRentals: plate/driverName/phone
            // - set status -> Completed OR WaitingDriverAccept (depends policy)
            await Task.CompletedTask;
            return RedirectToAction(nameof(Booking), new { id });
        }

        // POST: /Admin/ForceComplete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceComplete(long id, string? comment)
        {
            // TODO:
            // - terminal close by admin
            await Task.CompletedTask;
            return RedirectToAction(nameof(Booking), new { id });
        }

        // ===== Master Data =====

        // GET: /Admin/Vehicles
        [HttpGet]
        public async Task<IActionResult> Vehicles()
        {
            ViewData["ActiveNav"] = "AdminVehicles";
            await Task.CompletedTask;
            return View();
        }

        // GET: /Admin/Drivers
        [HttpGet]
        public async Task<IActionResult> Drivers()
        {
            ViewData["ActiveNav"] = "AdminDrivers";
            await Task.CompletedTask;
            return View();
        }

        // GET: /Admin/Users
        [HttpGet]
        public async Task<IActionResult> Users()
        {
            ViewData["ActiveNav"] = "AdminUsers";
            await Task.CompletedTask;
            return View();
        }

        // POST: /Admin/UpdateRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserRole(int userId, int roleFlags)
        {
            // TODO:
            // - update Users.RoleFlags
            await Task.CompletedTask;
            return RedirectToAction(nameof(Users));
        }


        [HttpGet("/Admin/drivers/create")]
        public async Task<IActionResult> CreateDriver()
        {
            ViewData["ActiveNav"] = "AdminDrivers";

            ViewBag.Vehicles = await _db.Vehicles.AsNoTracking()
                .Where(v => v.IsActive)
                .OrderBy(v => v.PlateNo)
                .Select(v => new { VehicleId = v.VehicleId, Display = v.PlateNo + " • " + v.VehicleType })
                .ToListAsync();

            return View(new CreateDriverVm { IsActive = true });
        }


        // POST: /admin/drivers/create
        [HttpPost("/Admin/drivers/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDriver([FromForm] CreateDriverVm vm)
        {
            ViewData["ActiveNav"] = "AdminDrivers";

            if (!ModelState.IsValid)
                return View(vm);

            var userCode = (vm.DriverCode ?? "").Trim(); // เช่น DRV001
            if (string.IsNullOrWhiteSpace(userCode))
            {
                ModelState.AddModelError(nameof(vm.DriverCode), "Driver code is required.");
                return View(vm);
            }

            // 1) validate not exists
            var existsUser = await _db.Users.AnyAsync(u => u.UserCode == userCode);
            if (existsUser)
            {
                ModelState.AddModelError(nameof(vm.DriverCode), "This DriverCode already exists.");
                return View(vm);
            }

            // 2) validate vehicle exists
            var vehicle = await _db.Vehicles.SingleOrDefaultAsync(v => v.VehicleId == vm.VehicleId && v.IsActive);
            if (vehicle is null)
            {
                ModelState.AddModelError(nameof(vm.VehicleId), "Vehicle not found or inactive.");
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
                IsActive = true,
                LastAssignedAtUtc = null,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _db.Drivers.Add(driver);

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Driver created: {userCode} (initial password: {initialPassword})";
            return RedirectToAction(nameof(CreateDriver));
        }

        public class CreateDriverVm
        {
            [Required]
            public string? DriverCode { get; set; } // DRV001

            public string? DriverNameTH { get; set; }
            public string? DriverNameEN { get; set; }

            [Required]
            public int VehicleId { get; set; }

            public bool IsActive { get; set; } = true;

            public string? InitialPassword { get; set; } // default Welcome@123
        }


        // ===== Models =====
        public class VendorQuoteModel
        {
            public string? VendorName { get; set; }
            public decimal? QuotedPrice { get; set; }
            public string? Note { get; set; }
        }

        public class VendorConfirmModel
        {
            public string? RentalPlateNo { get; set; }
            public string? RentalDriverName { get; set; }
            public string? RentalDriverPhone { get; set; }
        }
    }
}
