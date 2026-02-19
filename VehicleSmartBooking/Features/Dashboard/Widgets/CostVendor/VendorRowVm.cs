namespace VehicleSmartBooking.Features.Dashboard.Widgets.CostVendor;

public sealed class VendorRowVm
{
    public string VendorName { get; init; } = string.Empty;
    public int Trips { get; init; }
    public decimal TotalCost { get; init; }
    public decimal AverageCostPerTrip { get; init; }
    public DateTime? LastUsedAtUtc { get; init; }
}
