namespace VehicleSmartBooking.Features.Dashboard.Widgets.Drivers;

public sealed class DispatchOutcomeVm
{
    public int AcceptedCount { get; init; }
    public int DeclinedCount { get; init; }
    public int NoResponseCount { get; init; }
    public IReadOnlyList<DeclineReasonVm> TopDeclineReasons { get; init; } = Array.Empty<DeclineReasonVm>();
}

public sealed class DeclineReasonVm
{
    public string Reason { get; init; } = string.Empty;
    public int Count { get; init; }
}
