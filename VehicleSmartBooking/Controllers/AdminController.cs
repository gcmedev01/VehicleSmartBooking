using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
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

        private const int ROLE_USER = 1;
        private const int ROLE_DRIVER = 4;
        private const int ROLE_ADMIN = 2;
        private const int ROLE_APPROVER = 8;

        public AdminController(VehicleBookingDbContext db, ILogger<AdminController> logger, IPasswordHasher hasher)
        {
            _db = db;
            _logger = logger;
            _hasher = hasher;
        }

        // ===== Operations =====
        // GET: /Admin/Dashboard
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            ViewData["ActiveNav"] = "Dashboard";


            return View();
        }

        // GET: /Admin/Worklist
        [HttpGet]
        public async Task<IActionResult> Worklist()
        {
            ViewData["ActiveNav"] = "AdminWorklist";

            var statuses = new[] { BookingStatus.WaitingAdminVendorQuotation, BookingStatus.WaitingAdminVendorConfirm, BookingStatus.AdminActionRequired };

            var items = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.Requester)
                .Where(b => statuses.Contains(b.Status))
                .OrderByDescending(b => b.StartAtUtc)
                .ToListAsync();

            return View(items);
        }

        [Authorize]
        public async Task<IActionResult> AllBookings()
        {
            ViewData["ActiveNav"] = "AllTasks";

            var userCodeClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userCodeClaim))
            {
                return View(new List<Booking>());
            }
            var bookings = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.Requester)
                .OrderByDescending(b => b.StartAtUtc)
                .ToListAsync();

            return View(bookings);
        }

        // GET: /Admin/Booking/{id}
        [HttpGet]
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
        }

        // POST: /Admin/SendVendorQuote/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendVendorQuote(long id, VendorQuoteModel model)
        {
            var booking = await _db.Bookings.Include(b => b.ExternalRental).SingleOrDefaultAsync(b => b.BookingId == id);
            if (booking == null) return NotFound();

            if (model == null)
            {
                TempData["Error"] = "Invalid vendor data.";
                return RedirectToAction(nameof(Booking), new { id });
            }

            // basic validation
            if (string.IsNullOrWhiteSpace(model.VendorName) && model.QuotedPrice == null)
            {
                TempData["Error"] = "Vendor name or quoted price is required.";
                return RedirectToAction(nameof(Booking), new { id });
            }

            // upsert ExternalRental
            var now = DateTime.UtcNow;
            try
            {
                if (booking.ExternalRental == null)
                {
                    booking.ExternalRental = new ExternalRental
                    {
                        BookingId = booking.BookingId,
                        VendorName = model.VendorName,
                        QuotedPrice = model.QuotedPrice,
                        QuoteSentAtUtc = now,
                        UserDecision = ExternalUserDecision.Pending,
                        UserDecisionAtUtc = null,
                        Note = model.Note
                    };
                    _db.ExternalRentals.Add(booking.ExternalRental);
                }
                else
                {
                    booking.ExternalRental.VendorName = model.VendorName;
                    booking.ExternalRental.QuotedPrice = model.QuotedPrice;
                    booking.ExternalRental.QuoteSentAtUtc = now;
                    booking.ExternalRental.UserDecision = ExternalUserDecision.Pending;
                    booking.ExternalRental.UserDecisionAtUtc = null;
                    booking.ExternalRental.Note = model.Note;
                }

                booking.IsExternalRental = true;
                booking.Status = BookingStatus.WaitingUserVendorAccept;
                booking.UpdatedAtUtc = now;

                await _db.SaveChangesAsync();

                TempData["Success"] = "Vendor quote sent.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save vendor quote for booking {BookingId}", booking.BookingId);
                TempData["Error"] = "Failed to save vendor quote. See logs.";
            }

            return RedirectToAction(nameof(Booking), new { id });
        }

        // GET helper: redirect GET requests for SendVendorQuote to booking page
        [HttpGet("/Admin/SendVendorQuote/{id:long}")]
        public IActionResult SendVendorQuoteGet(long id)
        {
            return RedirectToAction(nameof(Booking), new { id });
        }

        // POST: /Admin/ConfirmVendor/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmVendor(long id, VendorConfirmModel model)
        {
            var booking = await _db.Bookings.Include(b => b.ExternalRental).SingleOrDefaultAsync(b => b.BookingId == id);
            if (booking == null) return NotFound();

            if (booking.ExternalRental == null)
            {
                booking.ExternalRental = new ExternalRental { BookingId = booking.BookingId };
                _db.ExternalRentals.Add(booking.ExternalRental);
            }

            booking.ExternalRental.RentalPlateNo = model.RentalPlateNo;
            booking.ExternalRental.RentalDriverName = model.RentalDriverName;
            booking.ExternalRental.RentalDriverPhone = model.RentalDriverPhone;
            booking.ExternalRental.AdminClosedAtUtc = null; // reopen

            booking.IsExternalRental = true;
            booking.UpdatedAtUtc = DateTime.UtcNow;
            booking.Status = BookingStatus.AdminActionRequired;

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Booking), new { id });
        }

        // POST: /Admin/ForceComplete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceComplete(long id)
        {
            var booking = await _db.Bookings.Include(b => b.ExternalRental).SingleOrDefaultAsync(b => b.BookingId == id);
            if (booking == null) return NotFound();

            booking.Status = BookingStatus.Completed;
            booking.UpdatedAtUtc = DateTime.UtcNow;

            if (booking.ExternalRental != null)
            {
                booking.ExternalRental.AdminClosedAtUtc = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Booking), new { id });
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
                            RoleFlags = ROLE_USER,
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

                var protectedRoles = ROLE_ADMIN | ROLE_DRIVER | ROLE_APPROVER;
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
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking is null) return NotFound();

            // mark layout active nav for Detail -> MyTask
            ViewData["ActiveNav"] = "AllTasks";

            return View(booking);
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
    }
}
