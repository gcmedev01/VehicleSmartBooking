using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Entities;

namespace VehicleBooking.Web.Domain.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly VehicleBookingDbContext _db;
    private readonly IHttpContextAccessor _http;

    public CurrentUserService(VehicleBookingDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task<User?> GetCurrentUserAsync(ClaimsPrincipal principal)
    {
        var userCode = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Try read from server-side session first to avoid DB hit
        try
        {
            var session = _http.HttpContext?.Session;
            var json = session?.GetString("CurrentUser");
            if (!string.IsNullOrWhiteSpace(json))
            {
                var doc = JsonSerializer.Deserialize<SessionUserDto>(json);
                if (doc != null && !string.IsNullOrWhiteSpace(doc.UserCode))
                {
                    if (string.Equals(doc.UserCode, userCode, StringComparison.OrdinalIgnoreCase))
                    {
                        // return a lightweight User instance (not tracked)
                        return new User
                        {
                            UserId = doc.UserId,
                            UserCode = doc.UserCode ?? string.Empty,
                            UsernameEN = doc.UsernameEN,
                            UsernameTH = doc.UsernameTH,
                            RoleFlags = doc.RoleFlags,
                            IsActive = true
                        };
                    }

                    session?.Remove("CurrentUser");
                }
            }
        }
        catch
        {
            // ignore session errors and fallback to claims
        }

        // Fallback: use claims (NameIdentifier = UserCode)
        if (string.IsNullOrWhiteSpace(userCode)) return null;

        return await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.UserCode == userCode && u.IsActive);
    }

    public async Task<Driver?> GetCurrentDriverAsync(ClaimsPrincipal principal)
    {
        var me = await GetCurrentUserAsync(principal);
        if (me is null) return null;

        return await _db.Drivers
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.UserId == me.UserId && d.IsActive);
    }

    private sealed class SessionUserDto
    {
        public string? UserCode { get; set; }
        public int UserId { get; set; }
        public string? UsernameEN { get; set; }
        public string? UsernameTH { get; set; }
        public int RoleFlags { get; set; }
        public string? DisplayName { get; set; }
    }
}
