namespace VehicleSmartBooking.Features.Dashboard;

public sealed class DashboardIndexViewModel
{
    public DashboardFilters Filters { get; init; } = new();
    public DashboardFilterOptionsViewModel FilterOptions { get; init; } = new();
    public IReadOnlyList<DashboardWidgetShellViewModel> Widgets { get; init; } = Array.Empty<DashboardWidgetShellViewModel>();
}
