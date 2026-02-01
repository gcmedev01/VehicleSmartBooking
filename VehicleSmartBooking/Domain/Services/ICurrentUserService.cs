using System.Security.Claims;
using VehicleBooking.Web.Domain.Entities;

namespace VehicleBooking.Web.Domain.Services;

public interface ICurrentUserService
{
    Task<User?> GetCurrentUserAsync(ClaimsPrincipal principal);
    Task<Driver?> GetCurrentDriverAsync(ClaimsPrincipal principal); // ✅ เพิ่ม
}
