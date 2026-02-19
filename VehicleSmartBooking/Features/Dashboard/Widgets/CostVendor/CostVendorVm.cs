namespace VehicleSmartBooking.Features.Dashboard.Widgets.CostVendor;

public sealed class CostVendorVm
{
    public IReadOnlyList<VendorRowVm> TopVendors { get; init; } = Array.Empty<VendorRowVm>();
}

public sealed class CostTrendVm
{
    public IReadOnlyList<CostTrendMonthVm> Months { get; init; } = Array.Empty<CostTrendMonthVm>();
}

public sealed class CostTrendMonthVm
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string Label { get; init; } = string.Empty;
    public decimal TotalCost { get; init; }
}
