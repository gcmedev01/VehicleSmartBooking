namespace VehicleSmartBooking.Features.Dashboard.Widgets.Kpis;

public sealed class KpiCardItemVm
{
    public string Title { get; init; } = string.Empty;
    public string ValueText { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public string? IconClass { get; init; }
    public string? Href { get; init; }
}
