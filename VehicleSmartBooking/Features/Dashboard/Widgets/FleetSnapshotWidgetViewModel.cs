namespace VehicleSmartBooking.Features.Dashboard.Widgets;

public sealed class FleetSnapshotWidgetViewModel
{
    public int ActiveVehicles { get; init; }
    public int AvailableVehicles { get; init; }
    public int MaintenanceVehicles { get; init; }
    public int OutOfServiceVehicles { get; init; }
    public int ActiveDrivers { get; init; }
}
