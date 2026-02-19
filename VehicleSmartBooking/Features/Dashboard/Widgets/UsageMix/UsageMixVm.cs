namespace VehicleSmartBooking.Features.Dashboard.Widgets.UsageMix;

public sealed class UsageMixVm
{
    public IReadOnlyList<UsageMixMonthVm> Months { get; init; } = Array.Empty<UsageMixMonthVm>();
}

public sealed class UsageMixMonthVm
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string Label { get; init; } = string.Empty;
    public int FleetTrips { get; init; }
    public int VendorTrips { get; init; }
    public int PersonalTrips { get; init; }
}
