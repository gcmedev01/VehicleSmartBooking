namespace VehicleSmartBooking.Features.Dashboard.Widgets.Kpis;

public sealed class KpiCardsVm
{
    public IReadOnlyList<KpiCardItemVm> Items { get; init; } = Array.Empty<KpiCardItemVm>();
}
