namespace VehicleSmartBooking.Features.Dashboard;

public sealed class DashboardWidgetShellViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string EndpointUrl { get; init; } = string.Empty;
    public string ColumnClass { get; init; } = "col-12";
}
