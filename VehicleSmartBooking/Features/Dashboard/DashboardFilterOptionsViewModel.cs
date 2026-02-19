namespace VehicleSmartBooking.Features.Dashboard;

public sealed class DashboardFilterOptionsViewModel
{
    public IReadOnlyList<string> DeptOptions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DivOptions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> FunctionOptions { get; init; } = Array.Empty<string>();
}
