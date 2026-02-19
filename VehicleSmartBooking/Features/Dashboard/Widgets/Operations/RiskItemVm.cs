using VehicleBooking.Web.Domain.Enums;

namespace VehicleSmartBooking.Features.Dashboard.Widgets.Operations;

public sealed class RiskItemVm
{
    public long BookingId { get; init; }
    public string RequesterName { get; init; } = string.Empty;
    public BookingStatus Status { get; init; }
    public DateTime StartAtUtc { get; init; }
    public string Reason { get; init; } = string.Empty;
}
