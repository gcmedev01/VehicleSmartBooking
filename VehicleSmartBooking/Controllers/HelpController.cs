using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VehicleSmartBooking.Controllers;

[Authorize]
public class HelpController : Controller
{
    // GET /Help/DriverNotificationGuide
    public IActionResult DriverNotificationGuide()
    {
        ViewData["ActiveNav"] = "DriverGuide";
        return View();
    }
}
