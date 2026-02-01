using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Auth;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Services;

namespace VehicleSmartBooking.Controllers
{
    [Route("account")]
    public class AccountController : Controller
    {
        private readonly VehicleBookingDbContext _db;
        private readonly ILogger<AccountController> _logger;
        private readonly IPasswordHasher _hasher;

        private const int MaxFailed = 5;

        // role flags
        private const int ROLE_USER = 1;
        private const int ROLE_ADMIN = 2;
        private const int ROLE_DRIVER = 4;
        private const int ROLE_APPROVER = 8;

        public AccountController(VehicleBookingDbContext db, ILogger<AccountController> logger, IPasswordHasher hasher)
        {
            _db = db;
            _logger = logger;
            _hasher = hasher;
        }

        // GET: /account/login
        [HttpGet("login")]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["Title"] = "Sign In";
            ViewData["ReturnUrl"] = returnUrl;
            return View(); // ใช้หน้าเดิมของคุณได้
        }

        // POST: /account/login (Local login for Driver)
        [HttpPost("login")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login([FromForm] LoginVm vm, string? returnUrl = null)
        {
            ViewData["Title"] = "Sign In";
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(vm);

            var username = (vm.Username ?? "").Trim();
            var password = vm.Password ?? "";

            var cred = await _db.UserCredentials
                .Include(x => x.User)
                .SingleOrDefaultAsync(x => x.LoginUsername == username);

            if (cred is null || cred.User is null || !cred.User.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Username or password is invalid.");
                return View(vm);
            }

            if (cred.IsLocked)
            {
                ModelState.AddModelError(string.Empty, "Account is locked. Please contact admin.");
                return View(vm);
            }

            var ok = _hasher.Verify(password, cred.PasswordHash, cred.PasswordSalt, cred.Iterations, cred.PasswordAlgo);

            if (!ok)
            {
                cred.FailedCount += 1;
                cred.LastFailedAtUtc = DateTime.UtcNow;

                if (cred.FailedCount >= MaxFailed)
                    cred.IsLocked = true;

                cred.UpdatedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                ModelState.AddModelError(string.Empty, "Username or password is invalid.");
                return View(vm);
            }

            // success => reset fail counters
            if (cred.FailedCount > 0 || cred.LastFailedAtUtc != null || cred.IsLocked)
            {
                cred.FailedCount = 0;
                cred.LastFailedAtUtc = null;
                cred.IsLocked = false;
                cred.UpdatedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            var user = cred.User;

            // สร้าง claims (ให้เหมือน SSO)
            var principal = ClaimsFactory.Create(user); // ✅ ใช้ของคุณเลย
            await HttpContext.SignInAsync(principal);

            // ถ้าเป็น driver และยังไม่เคยเปลี่ยนรหัส -> บังคับไปเปลี่ยน
            // RoleFlags ของคุณเป็น int (ไม่ใช่ nullable)
            var roleFlags = user.RoleFlags;

            if ((roleFlags & ROLE_DRIVER) == ROLE_DRIVER)
            {
                if (cred.PasswordChangedAtUtc == null)
                {
                    return RedirectToAction(nameof(ChangePassword));
                }
            }

            // returnUrl
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectAfterLogin(roleFlags);
        }

        // GET: /account/change-password
        [HttpGet("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword()
        {
            var me = await ResolveCurrentUserAsync();
            if (me is null) return Forbid();

            ViewData["Title"] = "Change Password";
            return View(new ChangePasswordVm());
        }

        // POST: /account/change-password
        [HttpPost("change-password")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword([FromForm] ChangePasswordVm vm)
        {
            var me = await ResolveCurrentUserAsync();
            if (me is null) return Forbid();

            ViewData["Title"] = "Change Password";

            if (!ModelState.IsValid)
                return View(vm);

            if (vm.NewPassword.Length < 8)
            {
                ModelState.AddModelError(nameof(vm.NewPassword), "Password must be at least 8 characters.");
                return View(vm);
            }

            if (vm.NewPassword != vm.ConfirmPassword)
            {
                ModelState.AddModelError(nameof(vm.ConfirmPassword), "Password confirmation does not match.");
                return View(vm);
            }

            var cred = await _db.UserCredentials.SingleOrDefaultAsync(x => x.UserId == me.UserId);
            if (cred is null) return Forbid();

            // ถ้าเคยเปลี่ยนแล้ว => ต้องใส่ current password ให้ถูก
            if (cred.PasswordChangedAtUtc != null)
            {
                var ok = _hasher.Verify(vm.CurrentPassword ?? "", cred.PasswordHash, cred.PasswordSalt, cred.Iterations, cred.PasswordAlgo);
                if (!ok)
                {
                    ModelState.AddModelError(nameof(vm.CurrentPassword), "Current password is invalid.");
                    return View(vm);
                }
            }

            var hashed = _hasher.Hash(vm.NewPassword);

            cred.PasswordHash = hashed.Hash;
            cred.PasswordSalt = hashed.Salt;
            cred.PasswordAlgo = hashed.Algo;
            cred.Iterations = hashed.Iterations;
            cred.PasswordChangedAtUtc = DateTime.UtcNow;

            cred.FailedCount = 0;
            cred.LastFailedAtUtc = null;
            cred.IsLocked = false;
            cred.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return RedirectAfterLogin(me.RoleFlags);
        }

        // POST: /account/logout (layout คุณเรียก Account/Logout)
        [HttpPost("logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // ---------------- helpers ----------------
        private IActionResult RedirectAfterLogin(int roleFlags)
        {
            bool isAdmin = (roleFlags & ROLE_ADMIN) == ROLE_ADMIN;
            bool isDriver = (roleFlags & ROLE_DRIVER) == ROLE_DRIVER;
            bool isApprover = (roleFlags & ROLE_APPROVER) == ROLE_APPROVER;

            if (isAdmin) return RedirectToAction("Fleet", "Admin");
            if (isDriver) return RedirectToAction("Dashboard", "Driver");
            if (isApprover) return RedirectToAction("Index", "Approvals");

            return RedirectToAction("Create", "Booking");
        }

        private async Task<User?> ResolveCurrentUserAsync()
        {
            // คุณใช้ ClaimTypes.NameIdentifier = UserCode
            var userCode = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User?.FindFirst("UserCode")?.Value;

            if (string.IsNullOrWhiteSpace(userCode))
                return null;

            return await _db.Users.SingleOrDefaultAsync(u => u.UserCode == userCode && u.IsActive);
        }

        // ---------------- VMs ----------------
        public class LoginVm
        {
            [Required]
            public string? Username { get; set; }

            [Required]
            public string? Password { get; set; }
        }

        public class ChangePasswordVm
        {
            public string? CurrentPassword { get; set; }

            [Required]
            public string NewPassword { get; set; } = "";

            [Required]
            public string ConfirmPassword { get; set; } = "";
        }
    }
}
