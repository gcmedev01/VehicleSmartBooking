namespace VehicleSmartBooking.Features.Dashboard.Widgets;

public sealed class ActivityExceptionsWidgetViewModel
{
    public int RecentActivityCount { get; init; }
    public int ExceptionCount { get; init; }
    public IReadOnlyList<ExceptionBookingItem> RecentExceptions { get; init; } = Array.Empty<ExceptionBookingItem>();
}

public sealed class ExceptionBookingItem
{
    public long BookingId { get; init; }
    public string RequesterName { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}
