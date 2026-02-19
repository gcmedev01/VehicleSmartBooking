using VehicleBooking.Web.Domain.Enums;

namespace VehicleSmartBooking.Features.Dashboard.Widgets.Fleet;

public sealed class FleetSnapshotVm
{
    public IReadOnlyList<VehicleUtilRowVm> TopVehicles { get; init; } = Array.Empty<VehicleUtilRowVm>();
}

public sealed class VehicleStatusVm
{
    public IReadOnlyList<VehicleStatusItemVm> Items { get; init; } = Array.Empty<VehicleStatusItemVm>();
}

public sealed class VehicleStatusItemVm
{
    public VehicleStatus Status { get; init; }
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
}
