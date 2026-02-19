using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleBooking.Web.Data;
using VehicleSmartBooking.Features.Dashboard;
using VehicleSmartBooking.Features.Dashboard.Queries;
using VehicleSmartBooking.Features.Dashboard.Widgets.CostVendor;
using VehicleSmartBooking.Features.Dashboard.Widgets.Drivers;
using VehicleSmartBooking.Features.Dashboard.Widgets.Fleet;
using VehicleSmartBooking.Features.Dashboard.Widgets.Kpis;
using VehicleSmartBooking.Features.Dashboard.Widgets.Operations;
using VehicleSmartBooking.Features.Dashboard.Widgets.UsageMix;

namespace VehicleSmartBooking.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AdminDashboardController : Controller
{
    private readonly IDashboardQueryService _dashboardQueryService;
    private readonly VehicleBookingDbContext _db;

    private static readonly IReadOnlyList<DashboardWidgetDefinition> WidgetRegistry =
    [
        new DashboardWidgetDefinition("kpi-cards", "KPI Cards", nameof(KpiCards), "col-12"),
        new DashboardWidgetDefinition("usage-mix", "Usage Mix", nameof(UsageMix), "col-12 col-lg-6"),
        new DashboardWidgetDefinition("cost-vendor", "Cost & Vendor", nameof(CostVendor), "col-12 col-lg-6"),
        new DashboardWidgetDefinition("fleet-snapshot", "Fleet Snapshot", nameof(Fleet), "col-12 col-lg-6"),
        new DashboardWidgetDefinition("driver-quality", "Driver Quality", nameof(DriverQuality), "col-12 col-lg-6"),
        new DashboardWidgetDefinition("activity", "Activity & Exceptions", nameof(Activity), "col-12")
    ];

    public AdminDashboardController(IDashboardQueryService dashboardQueryService, VehicleBookingDbContext db)
    {
        _dashboardQueryService = dashboardQueryService;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] DashboardFilters filters)
    {
        ViewData["ActiveNav"] = "Dashboard";

        var filterOptions = await _dashboardQueryService.GetFilterOptionsAsync();
        var widgets = WidgetRegistry
            .Select(definition => new DashboardWidgetShellViewModel
            {
                Id = definition.Id,
                Title = definition.Title,
                ColumnClass = definition.ColumnClass,
                EndpointUrl = Url.Action(definition.ActionName, "AdminDashboard") ?? string.Empty
            })
            .ToList();

        var vm = new DashboardIndexViewModel
        {
            Filters = filters,
            FilterOptions = filterOptions,
            Widgets = widgets
        };

        return View("Index", vm);
    }

    [HttpGet]
    public async Task<IActionResult> KpiCards([FromQuery] DashboardFilters filters)
    {
        KpiCardsVm vm = await _dashboardQueryService.GetKpiCardsAsync(filters);
        return PartialView("DashboardWidgets/Kpis/_KpiCards", vm);
    }

    [HttpGet]
    public IActionResult UsageMix()
    {
        return PartialView("DashboardWidgets/UsageMix/_UsageMix");
    }

    [HttpGet]
    public async Task<IActionResult> UsageMixData([FromQuery] DashboardFilters filters)
    {
        UsageMixVm vm = await _dashboardQueryService.GetUsageMixAsync(filters);
        return Json(vm);
    }

    [HttpGet]
    public async Task<IActionResult> TripScopeData([FromQuery] DashboardFilters filters)
    {
        TripScopeVm vm = await _dashboardQueryService.GetTripScopeMixAsync(filters);
        return Json(vm);
    }

    [HttpGet]
    public async Task<IActionResult> CostVendor([FromQuery] DashboardFilters filters)
    {
        var vendors = await _dashboardQueryService.GetTopVendorsAsync(filters, 10);
        var vm = new CostVendorVm
        {
            TopVendors = vendors
        };
        return PartialView("DashboardWidgets/CostVendor/_CostVendor", vm);
    }

    [HttpGet]
    public async Task<IActionResult> VendorCostTrendData([FromQuery] DashboardFilters filters)
    {
        var vm = await _dashboardQueryService.GetVendorCostTrendAsync(filters);
        return Json(vm);
    }

    [HttpGet]
    public async Task<IActionResult> PersonalCostTrendData([FromQuery] DashboardFilters filters)
    {
        var vm = await _dashboardQueryService.GetPersonalCostTrendAsync(filters);
        return Json(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Fleet([FromQuery] DashboardFilters filters)
    {
        var topVehicles = await _dashboardQueryService.GetTopUtilizedVehiclesAsync(filters, 10);
        var vm = new FleetSnapshotVm
        {
            TopVehicles = topVehicles
        };
        return PartialView("DashboardWidgets/Fleet/_FleetSnapshot", vm);
    }

    [HttpGet]
    public async Task<IActionResult> VehicleStatusData()
    {
        var vm = await _dashboardQueryService.GetVehicleStatusAsync();
        return Json(vm);
    }

    [HttpGet]
    public async Task<IActionResult> DriverQuality([FromQuery] DashboardFilters filters)
    {
        var topDrivers = await _dashboardQueryService.GetTopDriversAsync(filters, 5);
        var vm = new DriverQualityVm
        {
            TopDrivers = topDrivers
        };
        return PartialView("DashboardWidgets/Drivers/_DriverQuality", vm);
    }

    [HttpGet]
    public async Task<IActionResult> RatingDistData([FromQuery] DashboardFilters filters)
    {
        var vm = await _dashboardQueryService.GetRatingDistributionAsync(filters);
        return Json(vm);
    }

    [HttpGet]
    public async Task<IActionResult> DispatchOutcomeData([FromQuery] DashboardFilters filters)
    {
        var vm = await _dashboardQueryService.GetDispatchOutcomeAsync(filters);
        return Json(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Activity([FromQuery] DashboardFilters filters)
    {
        var activities = await _dashboardQueryService.GetActivityFeedAsync(filters, 30);
        var risks = await _dashboardQueryService.GetRiskListAsync(filters, 10, 6);
        var vm = new ActivityFeedVm
        {
            Activities = activities,
            Risks = risks
        };
        return PartialView("DashboardWidgets/Operations/_Activity", vm);
    }

    [HttpGet("/AdminDashboard/Booking/{id:long}")]
    public async Task<IActionResult> Booking(long id, string? returnUrl)
    {
        ViewData["ActiveNav"] = "AdminDashboard";
        var booking = await _db.Bookings
            .Include(b => b.Requester)
            .Include(b => b.Approvals)
                .ThenInclude(a => a.Approver)
            .Include(b => b.DispatchLogs)
                .ThenInclude(d => d.Driver)
                    .ThenInclude(dr => dr.User)
            .Include(b => b.DispatchLogs)
                .ThenInclude(d => d.Vehicle)
            .Include(b => b.ExternalRental)
            .Include(b => b.Attachments)
            .Include(b => b.Rating)
            .Include(b => b.AssignedDriver)
                .ThenInclude(d => d.User)
            .Include(b => b.AssignedVehicle)
            .FirstOrDefaultAsync(b => b.BookingId == id);

        if (booking == null)
        {
            return NotFound();
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View("BookingDetail", booking);
    }
}
