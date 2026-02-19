using VehicleBooking.Web.Domain.Enums;

namespace VehicleSmartBooking.Features.Dashboard;

public sealed class DashboardFilters
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Dept { get; set; }
    public string? Div { get; set; }
    public string? Function { get; set; }
    public string? Mode { get; set; }
    public TripType? TripScope { get; set; }
    public VehicleType? VehicleTypeRequested { get; set; }
    public BookingStatus? Status { get; set; }
}
