using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VehicleSmartBooking.Controllers
{
    [Authorize]
    public class QueueController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            ViewData["ActiveNav"] = "Queue";
            return View();
        }

        // GET: /Queue/Day?date=2026-01-30&type=van
        [HttpGet]
        public IActionResult Day(string date, string type)
        {
            ViewData["ActiveNav"] = "Queue";
            // TODO: return partial/json later
            return Ok(new { date, type });
        }
    }
}
