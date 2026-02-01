using Microsoft.AspNetCore.Mvc;

namespace VehicleSmartBooking.Controllers
{
    public class LoginController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
