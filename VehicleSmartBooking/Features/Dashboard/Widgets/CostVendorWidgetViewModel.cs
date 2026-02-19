namespace VehicleSmartBooking.Features.Dashboard.Widgets;

public sealed class CostVendorWidgetViewModel
{
    public decimal TotalExternalCost { get; init; }
    public int ExternalBookingCount { get; init; }
    public IReadOnlyList<VendorCostItem> TopVendors { get; init; } = Array.Empty<VendorCostItem>();
}

public sealed class VendorCostItem
{
    public string VendorName { get; init; } = string.Empty;
    public decimal TotalCost { get; init; }
    public int BookingCount { get; init; }
}
