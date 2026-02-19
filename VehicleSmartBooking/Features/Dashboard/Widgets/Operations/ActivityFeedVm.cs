namespace VehicleSmartBooking.Features.Dashboard.Widgets.Operations;

public sealed class ActivityFeedVm
{
    public IReadOnlyList<ActivityItemVm> Activities { get; init; } = Array.Empty<ActivityItemVm>();
    public IReadOnlyList<RiskItemVm> Risks { get; init; } = Array.Empty<RiskItemVm>();
}
