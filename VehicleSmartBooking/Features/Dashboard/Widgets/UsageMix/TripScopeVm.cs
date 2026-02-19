namespace VehicleSmartBooking.Features.Dashboard.Widgets.UsageMix;

public sealed class TripScopeVm
{
    public IReadOnlyList<TripScopeMonthVm> Months { get; init; } = Array.Empty<TripScopeMonthVm>();
}

public sealed class TripScopeMonthVm
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string Label { get; init; } = string.Empty;
    public int InProvinceTrips { get; init; }
    public int OutProvinceTrips { get; init; }
}
