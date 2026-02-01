using Microsoft.AspNetCore.Mvc;

namespace VehicleSmartBooking.Domain.Services
{
    public class BookingWorkflowService : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
