namespace VehicleSmartBooking.Features.Dashboard.Widgets.Drivers;

public sealed class DriverQualityVm
{
    public IReadOnlyList<TopDriverRowVm> TopDrivers { get; init; } = Array.Empty<TopDriverRowVm>();
}

public sealed class TopDriverRowVm
{
    public string DriverName { get; init; } = string.Empty;
    public int Trips { get; init; }
    public double AverageRating { get; init; }
    public double AcceptRate { get; init; }
}
