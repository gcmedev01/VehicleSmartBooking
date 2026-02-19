using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Auth;
using Microsoft.Extensions.Options;
using VehicleBooking.Web.Domain.Options;
using System.Text.Json;

namespace VehicleSmartBooking.Controllers
{
    [Route("auth")]
    public class AuthController : Controller
    {
        private readonly VehicleBookingDbContext _db;
        private readonly ILogger<AuthController> _logger;
        private readonly SsoOptions _ssoOptions;

        public AuthController(VehicleBookingDbContext db, ILogger<AuthController> logger, IOptions<SsoOptions> ssoOptions)
        {
            _db = db;
            _logger = logger;
            _ssoOptions = ssoOptions.Value;
        }

        [HttpGet("sso/login")]
        public IActionResult SsoLogin(string? returnUrl = null)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                HttpContext.Session.SetString("ReturnUrl", returnUrl);
            }

            var baseUrl = _ssoOptions.BaseUrl?.TrimEnd('/') ?? "";
            var signInPath = _ssoOptions.SignInPath ?? "";
            var callback = _ssoOptions.CallbackUrl ?? "";

            if (string.IsNullOrWhiteSpace(baseUrl) ||
                string.IsNullOrWhiteSpace(signInPath) ||
                string.IsNullOrWhiteSpace(callback))
            {
                _logger.LogError("SSO options are missing. Please check appsettings.json section 'Sso'.");
                return StatusCode(StatusCodes.Status500InternalServerError, "SSO is not configured.");
            }

            var redirectUrl = $"{baseUrl}{signInPath}{Uri.EscapeDataString(callback)}";
            return Redirect(redirectUrl);
        }

        [HttpGet("sso/callback")]
        public Task<IActionResult> SsoCallbackQuery([FromQuery] string id, [FromQuery] string token)
            => SsoCallbackCore(id, token);

        [HttpGet("sso/callback/{id}/{*token}")]
        public Task<IActionResult> SsoCallbackPath(string id, string token)
            => SsoCallbackCore(id, token);

        private async Task<IActionResult> SsoCallbackCore(string id, string token)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(token))
                return RedirectToAction("NotPermission", "Home");

            _logger.LogInformation("SSO callback received. id={Id}", id);

            // WARNING: token validation intentionally skipped (accepted risk).
            _logger.LogWarning("SSO token validation skipped for id={Id}.", id);

            var employeeId = id;

            var user = await _db.Users.SingleOrDefaultAsync(u => u.UserCode == employeeId && u.IsActive);
            if (user is null)
            {
                _logger.LogWarning("User not found or inactive. UserCode={UserCode}", employeeId);
                return RedirectToAction("NotPermission", "Home");
            }

            var principal = ClaimsFactory.Create(user);

            // Sign in using cookie authentication to keep ASP.NET authorization working.
            await HttpContext.SignInAsync(principal);

            var returnUrl = HttpContext.Session.GetString("ReturnUrl");

            // Also persist selected claim information in server-side session (JSON) and avoid storing everything in cookie.
            try
            {
                HttpContext.Session.Clear();
                var sessionObj = new Dictionary<string, object?>
                {
                    ["UserCode"] = user.UserCode,
                    ["UserId"] = user.UserId,
                    ["UsernameEN"] = user.UsernameEN,
                    ["UsernameTH"] = user.UsernameTH,
                    ["DisplayName"] = !string.IsNullOrWhiteSpace(user.UsernameTH) ? user.UsernameTH : (user.UsernameEN ?? user.UserCode),
                    ["RoleFlags"] = user.RoleFlags
                };

                var json = JsonSerializer.Serialize(sessionObj);
                HttpContext.Session.SetString("CurrentUser", json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write user info into session for UserCode={UserCode}", user.UserCode);
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                HttpContext.Session.Remove("ReturnUrl");
                return Redirect(returnUrl);
            }

            return RedirectAfterLogin(user);
        }

        private IActionResult RedirectAfterLogin(VehicleBooking.Web.Domain.Entities.User user)
        {
            int roleFlags = user.RoleFlags;

            bool isDriver = (roleFlags & 4) == 4;

            if (isDriver)
            {
                var hasDriverProfile = _db.Drivers.AsNoTracking().Any(d => d.UserId == user.UserId && d.IsActive);
                if (hasDriverProfile)
                {
                    return RedirectToAction("MyJobs", "Driver");
                }

                return RedirectToAction("NotPermission", "Home");
            }

            return RedirectToAction("Create", "Booking");
        }

        [HttpPost("logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Clear server-side session data
            try
            {
                HttpContext.Session.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear session during logout.");
            }

            // Sign out authentication cookie
            await HttpContext.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}
