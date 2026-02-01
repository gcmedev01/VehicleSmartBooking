using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Entities;

namespace VehicleBooking.Web.Domain.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly VehicleBookingDbContext _db;

    public CurrentUserService(VehicleBookingDbContext db)
    {
        _db = db;
    }

    public async Task<User?> GetCurrentUserAsync(ClaimsPrincipal principal)
    {
        // คุณบอกแล้วว่า ClaimTypes.NameIdentifier = UserCode
        var userCode = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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
}
