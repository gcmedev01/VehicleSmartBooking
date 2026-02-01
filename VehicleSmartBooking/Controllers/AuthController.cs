using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Auth;
using Microsoft.Extensions.Options;
using VehicleBooking.Web.Domain.Options;

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
        public IActionResult SsoLogin()
        {
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
            await HttpContext.SignInAsync(principal);

            return RedirectAfterLogin(user);
        }

        private IActionResult RedirectAfterLogin(dynamic user)
        {
            int roleFlags = (int)user.RoleFlags;

            bool isAdmin = (roleFlags & 2) == 2;
            bool isDriver = (roleFlags & 4) == 4;
            bool isApprover = (roleFlags & 8) == 8;

            if (isAdmin) return RedirectToAction("Fleet", "Admin");
            if (isDriver) return RedirectToAction("Dashboard", "Driver");
            if (isApprover) return RedirectToAction("Index", "Approvals");

            return RedirectToAction("Create", "Booking");
        }

        [HttpPost("logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}
