namespace VehicleSmartBooking.Features.Dashboard.Widgets;

public sealed class DriverQualityWidgetViewModel
{
    public double AverageScore { get; init; }
    public int RatingCount { get; init; }
    public IReadOnlyList<DriverRatingSummary> RecentRatings { get; init; } = Array.Empty<DriverRatingSummary>();
}

public sealed class DriverRatingSummary
{
    public string DriverName { get; init; } = string.Empty;
    public double AverageScore { get; init; }
    public DateTime RatedAtUtc { get; init; }
}
