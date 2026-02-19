namespace VehicleSmartBooking.Features.Dashboard.Widgets;

public sealed class KpiCardsWidgetViewModel
{
    public int TotalBookings { get; init; }
    public int ActiveBookings { get; init; }
    public int CompletedBookings { get; init; }
    public int CancelledBookings { get; init; }
}
