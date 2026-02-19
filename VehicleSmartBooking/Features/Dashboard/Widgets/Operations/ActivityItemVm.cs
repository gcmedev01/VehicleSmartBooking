namespace VehicleSmartBooking.Features.Dashboard.Widgets.Operations;

public sealed class ActivityItemVm
{
    public long BookingId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public DateTime OccurredAtUtc { get; init; }
}
