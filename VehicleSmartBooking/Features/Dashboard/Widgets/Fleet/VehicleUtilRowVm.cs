using VehicleBooking.Web.Domain.Enums;

namespace VehicleSmartBooking.Features.Dashboard.Widgets.Fleet;

public sealed class VehicleUtilRowVm
{
    public string PlateNo { get; init; } = string.Empty;
    public VehicleType VehicleType { get; init; }
    public int TripsCount { get; init; }
    public double TotalHours { get; init; }
    public DateTime? LastTripAtUtc { get; init; }
}
