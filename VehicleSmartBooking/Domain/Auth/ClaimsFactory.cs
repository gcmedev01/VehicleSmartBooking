using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using VehicleBooking.Web.Domain.Entities;

namespace VehicleBooking.Web.Domain.Auth;

public static class ClaimsFactory
{
    // role flags (ปรับให้ตรงของคุณได้)
    private const int ROLE_USER = 1;
    private const int ROLE_ADMIN = 2;
    private const int ROLE_DRIVER = 4;
    private const int ROLE_APPROVER = 8;

    public static ClaimsPrincipal Create(User user)
    {
        var claims = new List<Claim>
    {
        // identity หลัก
        new Claim(ClaimTypes.NameIdentifier, user.UserCode),
        new Claim(ClaimTypes.Name, user.UsernameTH ?? user.UsernameEN ?? user.UserCode),

        // custom claims สำหรับ UI
        new Claim("UserCode", user.UserCode),
        new Claim("UsernameTH", user.UsernameTH ?? ""),
        new Claim("UsernameEN", user.UsernameEN ?? ""),
        new Claim("DisplayName", user.UsernameEN ?? user.UsernameTH ?? user.UserCode),
    };

        var isDriver = (user.RoleFlags & ROLE_DRIVER) != 0;
        if (isDriver)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Driver"));
        }
        else
        {
            // map RoleFlags -> role claims
            if ((user.RoleFlags & ROLE_USER) != 0) claims.Add(new Claim(ClaimTypes.Role, "User"));
            if ((user.RoleFlags & ROLE_ADMIN) != 0) claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            if ((user.RoleFlags & ROLE_APPROVER) != 0) claims.Add(new Claim(ClaimTypes.Role, "Approver"));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }
}
